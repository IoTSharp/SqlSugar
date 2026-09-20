using System.Data;
using System.Linq;
using SqlSugar;
using IsolationLevel = System.Data.IsolationLevel;

var databasePath = Path.Combine(Path.GetTempPath(), "sqlsugar-sonnetdb-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(databasePath);

try
{
    var db = new SqlSugarClient(new ConnectionConfig
    {
        ConnectionString = "Data Source=" + databasePath,
        DbType = SqlSugar.DbType.SonnetDB,
        InitKeyType = InitKeyType.Attribute,
        IsAutoCloseConnection = true
    });

    db.Aop.OnLogExecuting = (sql, _) => Console.WriteLine("执行 SQL：" + sql.Replace(Environment.NewLine, " "));

    验证CodeFirst与元数据(db);
    验证默认值演进(db);
    验证自增键类型(db);
    验证保留大小写标识符(db);
    验证特殊列名单行插入(db);
    验证Fastest批量写入(db);
    验证DateOnly批量写入(db);
    var identity = 验证插入与查询(db);
    验证Json字段访问(db);
    验证Storageable与批量合并(db);
    验证批量自增键边界(db);
    验证更新与删除(db, identity);
    验证分页与函数(db);
    验证分表能力边界(db);
    验证组合查询(db);
    验证事务(db);
    验证提供程序翻译边界(db);
    验证多表分页边界(db);
    验证方言边界(db);
    验证批量主键列表(db);

    db.Dispose();
    Console.WriteLine("SonnetDB 冒烟验证已通过：代码优先、增删改查、分页、元数据、函数和关系表事务均已验证。");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine("SonnetDB 冒烟验证失败：" + exception.Message);
    return 1;
}
finally
{
    try
    {
        if (Directory.Exists(databasePath))
        {
            Directory.Delete(databasePath, recursive: true);
        }
    }
    catch
    {
        Console.Error.WriteLine("无法删除冒烟验证临时数据库目录，请在进程退出后手动清理：" + databasePath);
    }
}

static void 验证CodeFirst与元数据(SqlSugarClient db)
{
    Console.WriteLine("开始：代码优先与元数据");
    db.CodeFirst.InitTables<SmokeDevice>();

    断言(db.DbMaintenance.IsAnyTable("sonnet_smoke_devices"), "代码优先未创建 sonnet_smoke_devices 表。");
    var columns = db.DbMaintenance.GetColumnInfosByTableName("sonnet_smoke_devices", false);
    断言(columns.Any(it => it.DbColumnName.Equals("id", StringComparison.OrdinalIgnoreCase) && it.IsPrimarykey && it.IsIdentity), "元数据未返回自增主键 id。");
    断言(columns.Any(it => it.DbColumnName.Equals("metadata", StringComparison.OrdinalIgnoreCase) && it.DataType.Equals("JSON", StringComparison.OrdinalIgnoreCase)), "元数据未返回 JSON 列 metadata。");
    断言(db.DbMaintenance.IsIdentity("sonnet_smoke_devices", "id"), "IsIdentity 未识别自增主键id。");
    断言(!db.DbMaintenance.IsIdentity("sonnet_smoke_devices", "name"), "IsIdentity 不应将普通列标记为自增列。");

    var schemaTable = db.Ado.GetDataTable(
        "SELECT table_name FROM information_schema.tables WHERE table_name = @tableName",
        new SugarParameter("@tableName", "sonnet_smoke_devices"));
    断言(schemaTable.Rows.Count == 1, "information_schema.tables 未返回刚创建的表。");

    const string indexName = "idx_sonnet_smoke_devices_name";
    断言(db.DbMaintenance.CreateIndex("sonnet_smoke_devices", ["Name"], indexName), "CreateIndex 未返回成功。");
    断言(db.DbMaintenance.IsAnyIndex(indexName), "DbMaintenance 未返回刚创建的索引。");
    var schemaIndex = db.Ado.GetDataTable(
        "SELECT index_name FROM information_schema.indexes WHERE table_name = @tableName AND index_name = @indexName",
        new SugarParameter("@tableName", "sonnet_smoke_devices"),
        new SugarParameter("@indexName", indexName));
    断言(schemaIndex.Rows.Count == 1, "information_schema.indexes 未返回刚创建的索引。");

    db.Ado.ExecuteCommand(
        "CREATE VIEW \"sonnet_smoke_view\" AS SELECT \"Id\", \"Name\" FROM \"sonnet_smoke_devices\"");
    var views = db.DbMaintenance.GetViewInfoList(false);
    断言(
        views.Any(it => it.DbObjectType == DbObjectType.View &&
                       it.Name.Equals("sonnet_smoke_view", StringComparison.OrdinalIgnoreCase)),
        "DbMaintenance 未返回刚创建的视图。");
}

static void 验证默认值演进(SqlSugarClient db)
{
    Console.WriteLine("开始：CodeFirst 默认值演进");
    db.CodeFirst.InitTables<默认值设备>();
    var initial = db.DbMaintenance.GetColumnInfosByTableName("sonnet_default_evolution", false)
        .Single(it => it.DbColumnName.Equals("Site", StringComparison.OrdinalIgnoreCase));
    断言(!string.IsNullOrWhiteSpace(initial.DefaultValue), "CodeFirst 未创建列默认值。");

    db.CodeFirst.InitTables<大小写默认值设备>();
    var caseUpdated = db.DbMaintenance.GetColumnInfosByTableName("sonnet_default_evolution", false)
        .Single(it => it.DbColumnName.Equals("Site", StringComparison.OrdinalIgnoreCase));
    断言(caseUpdated.DefaultValue?.Contains("North", StringComparison.Ordinal) == true,
        "STRING 默认值比较不应忽略大小写。");

    db.CodeFirst.InitTables<移除默认值设备>();
    var updated = db.DbMaintenance.GetColumnInfosByTableName("sonnet_default_evolution", false)
        .Single(it => it.DbColumnName.Equals("Site", StringComparison.OrdinalIgnoreCase));
    断言(string.IsNullOrWhiteSpace(updated.DefaultValue), "CodeFirst 移除默认值后，数据库仍保留旧默认值。");

    db.CodeFirst.InitTables<字符串数字默认值设备>();
    db.Ado.ExecuteCommand("INSERT INTO \"sonnet_string_numeric_default\" (\"Id\") VALUES (1)");
    断言(
        db.Queryable<字符串数字默认值设备>().Single().Text == "1",
        "STRING 默认值为数字文本时必须按字符串保存。");
}

static void 验证自增键类型(SqlSugarClient db)
{
    Console.WriteLine("开始：自增键 CLR 类型");
    db.CodeFirst.InitTables<UnsignedIdentitySmokeDevice>();
    var device = new UnsignedIdentitySmokeDevice { Name = "unsigned" };
    断言(
        db.Insertable(device).ExecuteCommandIdentityIntoEntity() && device.Id == 1,
        "无符号自增主键未正确回写实体。");

    var asyncDevice = new UnsignedIdentitySmokeDevice { Name = "unsigned-async" };
    断言(
        db.Insertable(asyncDevice).ExecuteCommandIdentityIntoEntityAsync().GetAwaiter().GetResult() &&
        asyncDevice.Id == 2,
        "异步无符号自增主键未正确回写实体。");

    db.CodeFirst.InitTables<LongIdentitySmokeDevice>();
    var longDevice = new LongIdentitySmokeDevice { Name = "long" };
    断言(
        db.Insertable(longDevice).ExecuteCommandIdentityIntoEntity() && longDevice.Id == 1,
        "长整型自增主键未正确回写实体。");
}

static int 验证插入与查询(SqlSugarClient db)
{
    Console.WriteLine("开始：插入与查询");
    var device = new SmokeDevice
    {
        Name = "pump",
        Amount = 12.5,
        Enabled = true,
        CreatedAt = new DateTime(2026, 9, 19, 4, 0, 0, DateTimeKind.Utc),
        Metadata = "{\"site\":\"north\",\"tags\":[\"pump\",\"edge\"]}",
        Payload = [1, 2, 3]
    };

    var identity = db.Insertable(device).ExecuteReturnIdentity();
    断言(identity > 0, "插入后未返回有效的自增主键。");

    var inserted = db.Insertable(new List<SmokeDevice>
    {
        new() { Name = "fan", Amount = 4.25, Enabled = false, CreatedAt = device.CreatedAt },
        new() { Name = "valve", Amount = 8.75, Enabled = true, CreatedAt = device.CreatedAt }
    }).ExecuteCommand();
    断言(inserted == 2, "批量插入的受影响行数不是 2。");

    var loaded = db.Queryable<SmokeDevice>().InSingle(identity);
    断言(loaded != null, "按主键查询未返回插入的数据。");
    断言(loaded.Name == "pump" && loaded.Enabled && Math.Abs(loaded.Amount - 12.5) < 0.0001, "查询出的基础字段与插入值不一致。");
    断言(loaded.Metadata == "{\"site\":\"north\",\"tags\":[\"pump\",\"edge\"]}", "查询出的 JSON 字段与插入值不一致。");
    断言(loaded.Payload is { Length: 3 } && loaded.Payload[0] == 1 && loaded.Payload[1] == 2 && loaded.Payload[2] == 3, "查询出的 BLOB 字段与插入值不一致。");

    return identity;
}

static void 验证保留大小写标识符(SqlSugarClient db)
{
    Console.WriteLine("开始：保留大小写标识符");
    db.CodeFirst.InitTables<大小写设备>();

    var schema = db.Ado.GetDataTable(
        "SELECT table_name FROM information_schema.tables WHERE table_name = @tableName",
        new SugarParameter("@tableName", "SonnetMixedCase"));
    断言(schema.Rows.Count == 1, "代码优先未保留 SonnetMixedCase 表名的大小写。");
    断言(
        db.DbMaintenance.GetColumnInfosByTableName("sonnetmixedcase", false).Count == 2 &&
        db.DbMaintenance.IsAnyColumn("sonnetmixedcase", "recordid", false),
        "混合大小写表名的元数据查询应遵循 SqlSugar 的大小写不敏感规则。");

    db.Insertable(new 大小写设备 { RecordId = 1, DisplayName = "保留大小写" }).ExecuteCommand();
    var loaded = db.Queryable<大小写设备>().InSingle(1);
    断言(loaded?.DisplayName == "保留大小写", "混合大小写的表名或列名未能完成查询。");
}

static void 验证特殊列名单行插入(SqlSugarClient db)
{
    Console.WriteLine("开始：特殊列名单行插入");
    db.CodeFirst.InitTables<特殊列名设备>();

    var count = db.Insertable(new 特殊列名设备 { Id = 1, OrderId = "order-1" }).ExecuteCommand();
    断言(count == 1, "包含连字符列名的单行插入返回的受影响行数不正确。");

    var loaded = db.Queryable<特殊列名设备>().InSingle(1);
    断言(loaded?.OrderId == "order-1", "包含连字符的列名未能完成单行插入和查询。");

    var updated = db.Updateable<特殊列名设备>()
        .SetColumns(device => device.OrderId == "order-2")
        .Where(device => device.Id == 1)
        .ExecuteCommand();
    断言(updated == 1, "包含连字符列名的更新返回的受影响行数不正确。");

    var entityUpdated = db.Updateable(new 特殊列名设备 { Id = 1, OrderId = "order-3" })
        .WhereColumns(device => device.Id)
        .ExecuteCommand();
    断言(entityUpdated == 1, "实体更新包含连字符列名时返回的受影响行数不正确。");

    var deleted = db.Deleteable<特殊列名设备>().Where(
        new List<IConditionalModel>
        {
            new ConditionalModel
            {
                FieldName = "order-id",
                ConditionalType = ConditionalType.Equal,
                FieldValue = "order-3"
            }
        }).ExecuteCommand();
    断言(deleted == 1, "条件删除包含连字符列名时返回的受影响行数不正确。");
}

static void 验证批量自增键边界(SqlSugarClient db)
{
    Console.WriteLine("开始：批量自增键边界");
    var before = db.Queryable<SmokeDevice>().Count();

    必须抛出批量自增键异常(
        "批量 ExecuteReturnIdentity",
        () => db.Insertable(创建批量设备()).ExecuteReturnIdentity());
    必须抛出批量自增键异常(
        "批量 ExecuteReturnIdentityAsync",
        () => db.Insertable(创建批量设备()).ExecuteReturnIdentityAsync().GetAwaiter().GetResult());
    必须抛出批量自增键异常(
        "批量 ExecuteReturnBigIdentity",
        () => db.Insertable(创建批量设备()).ExecuteReturnBigIdentity());
    必须抛出批量自增键异常(
        "批量 ExecuteReturnBigIdentityAsync",
        () => db.Insertable(创建批量设备()).ExecuteReturnBigIdentityAsync().GetAwaiter().GetResult());
    必须抛出批量自增键异常(
        "批量 ExecuteCommandIdentityIntoEntity",
        () => db.Insertable(创建批量设备()).ExecuteCommandIdentityIntoEntity());
    必须抛出批量自增键异常(
        "批量 ExecuteCommandIdentityIntoEntityAsync",
        () => db.Insertable(创建批量设备()).ExecuteCommandIdentityIntoEntityAsync().GetAwaiter().GetResult());

    断言(
        db.Queryable<SmokeDevice>().Count() == before,
        "批量返回自增键被拒绝时不应写入任何数据。");
}

static void 验证Fastest批量写入(SqlSugarClient db)
{
    Console.WriteLine("开始：Fastest 批量写入");
    db.CodeFirst.InitTables<FastSmokeDevice>();

    var nonUtcTime = new DateTimeOffset(2026, 9, 19, 12, 30, 0, TimeSpan.FromHours(8));
    var syncRows = new List<FastSmokeDevice>
    {
        new() { Name = "fast-sync-1", Amount = 1.5, OccurredAt = nonUtcTime, ClockValue = "08:30" },
        new() { Name = "fast-sync-2", Amount = 2.5, OccurredAt = nonUtcTime, ClockValue = "08:31" }
    };
    var syncCount = db.Fastest<FastSmokeDevice>().BulkCopy(syncRows);
    断言(syncCount == syncRows.Count, "Fastest.BulkCopy 返回的受影响行数不正确。");
    断言(读取Fastest时间(db, "fast-sync-1") == nonUtcTime.UtcDateTime, "Fastest.BulkCopy 未将非 UTC DateTimeOffset 归一化为 UTC。");

    var asyncTime = new DateTimeOffset(2026, 9, 20, 1, 15, 0, TimeSpan.FromHours(-7));
    var asyncRows = new List<FastSmokeDevice>
    {
        new() { Name = "fast-async-1", Amount = 3.5, OccurredAt = asyncTime, ClockValue = "01:15" },
        new() { Name = "fast-async-2", Amount = 4.5, OccurredAt = asyncTime, ClockValue = "01:16" }
    };
    var asyncCount = db.Fastest<FastSmokeDevice>().BulkCopyAsync(asyncRows).GetAwaiter().GetResult();
    断言(asyncCount == asyncRows.Count, "Fastest.BulkCopyAsync 返回的受影响行数不正确。");
    断言(读取Fastest时间(db, "fast-async-1") == asyncTime.UtcDateTime, "Fastest.BulkCopyAsync 未将非 UTC DateTimeOffset 归一化为 UTC。");

    db.Ado.BeginTran();
    try
    {
        var transactionRows = new List<FastSmokeDevice>
        {
            new() { Name = "fast-rollback", Amount = 5.5, OccurredAt = nonUtcTime, ClockValue = "08:32" }
        };
        var transactionCount = db.Fastest<FastSmokeDevice>().BulkCopyAsync(transactionRows).GetAwaiter().GetResult();
        断言(transactionCount == 1, "事务内 Fastest.BulkCopyAsync 返回的受影响行数不正确。");
        断言(db.Queryable<FastSmokeDevice>().Where(it => it.Name == "fast-rollback").Count() == 1, "事务内无法读取 Fastest 批量写入的数据。");
        db.Ado.RollbackTran();
    }
    catch
    {
        db.Ado.RollbackTran();
        throw;
    }

    断言(db.Queryable<FastSmokeDevice>().Where(it => it.Name == "fast-rollback").Count() == 0, "Fastest 批量写入在事务回滚后仍然可见。");

    const int explicitIdentity = 5000;
    var offIdentityCount = db.Fastest<FastSmokeDevice>().OffIdentity().BulkCopy(new List<FastSmokeDevice>
    {
        new() { Id = explicitIdentity, Name = "fast-explicit-identity", Amount = 6.5, OccurredAt = nonUtcTime, ClockValue = "08:33" }
    });
    断言(offIdentityCount == 1, "Fastest.OffIdentity().BulkCopy 返回的受影响行数不正确。");
    断言(
        db.Queryable<FastSmokeDevice>().Where(it => it.Name == "fast-explicit-identity").Single().Id == explicitIdentity,
        "Fastest.OffIdentity() 未写入显式自增键。");

    var autoIdentityCount = db.Fastest<FastSmokeDevice>().BulkCopy(new List<FastSmokeDevice>
    {
        new() { Name = "fast-after-explicit-identity", Amount = 7.5, OccurredAt = nonUtcTime, ClockValue = "08:34" }
    });
    断言(autoIdentityCount == 1, "显式自增键后的 Fastest.BulkCopy 返回的受影响行数不正确。");
    var autoIdentity = db.Queryable<FastSmokeDevice>()
        .Where(it => it.Name == "fast-after-explicit-identity")
        .Single()
        .Id;
    断言(autoIdentity > explicitIdentity, "显式自增键写入后，SonnetDB 未推进自增序列。");

    var dataTableTime = new DateTimeOffset(2026, 9, 20, 9, 45, 0, TimeSpan.FromHours(-7));
    var dataTable = new DataTable();
    dataTable.TableName = "caller_table_name";
    dataTable.Columns.Add(nameof(FastSmokeDevice.Name), typeof(string));
    dataTable.Columns.Add(nameof(FastSmokeDevice.Amount), typeof(double));
    dataTable.Columns.Add(nameof(FastSmokeDevice.OccurredAt), typeof(DateTimeOffset));
    dataTable.Columns.Add(nameof(FastSmokeDevice.ClockValue), typeof(string));
    dataTable.Columns.Add("忽略列", typeof(string));
    dataTable.Rows.Add("fast-datatable", 8.5, dataTableTime, "08:35", "该列不应写入");
    var sourceColumnNames = dataTable.Columns.Cast<DataColumn>().Select(column => column.ColumnName).ToArray();
    var dataTableCount = db.Fastest<FastSmokeDevice>().AS("sonnet_fast_smoke_devices").BulkCopy(dataTable);
    断言(dataTableCount == 1, "Fastest DataTable 批量写入返回的受影响行数不正确。");
    断言(
        dataTable.Columns.Cast<DataColumn>().Select(column => column.ColumnName).SequenceEqual(sourceColumnNames),
        "Fastest DataTable 批量写入不应删除调用方的列。");
    断言(dataTable.TableName == "caller_table_name", "Fastest DataTable 批量写入不应修改调用方的表名。");
    断言(
        读取Fastest时间(db, "fast-datatable") == dataTableTime.UtcDateTime,
        "Fastest DataTable 批量写入未将非 UTC DateTimeOffset 归一化为 UTC。");

    var beforeUnsupportedFastest = db.Queryable<FastSmokeDevice>().Count();
    必须抛出中文不支持异常(
        "Fastest 小数类型",
        () => db.Fastest<FastDecimalDevice>().BulkCopy(new List<FastDecimalDevice>
        {
            new() { Name = "fast-decimal", Amount = 1.25m, OccurredAt = nonUtcTime, ClockValue = "08:33" }
        }));
    必须抛出中文不支持异常(
        "Fastest 仅时间类型",
        () => db.Fastest<FastTimeOnlyDevice>().BulkCopyAsync(new List<FastTimeOnlyDevice>
        {
            new() { Name = "fast-time", Amount = 1.25, OccurredAt = nonUtcTime, ClockValue = new TimeOnly(8, 34) }
        }).GetAwaiter().GetResult());
    必须抛出中文不支持异常(
        "Fastest 跨数据库表写入",
        () => db.Fastest<FastSmokeDevice>()
            .AS("other_database.sonnet_fast_smoke_devices")
            .BulkCopy(new List<FastSmokeDevice>
            {
                new() { Name = "fast-cross-database", Amount = 1.25, OccurredAt = nonUtcTime, ClockValue = "08:36" }
            }));
    断言(
        db.Queryable<FastSmokeDevice>().Count() == beforeUnsupportedFastest,
        "Fastest 不支持的列类型被拒绝时不应留下部分写入的数据。");
}

static void 验证DateOnly批量写入(SqlSugarClient db)
{
    Console.WriteLine("开始：DateOnly 批量写入");
    db.CodeFirst.InitTables<DateOnlySmokeDevice>();

    var rows = new List<DateOnlySmokeDevice>
    {
        new() { Name = "date-only-first", OccurredOn = new DateOnly(2026, 9, 19) },
        new() { Name = "date-only-second", OccurredOn = new DateOnly(2026, 9, 20) }
    };
    var count = db.Insertable(rows).ExecuteCommand();
    断言(count == rows.Count, "DateOnly 批量插入返回的受影响行数不正确。");

    var first = db.Queryable<DateOnlySmokeDevice>()
        .Where(it => it.Name == "date-only-first")
        .Single();
    var second = db.Queryable<DateOnlySmokeDevice>()
        .Where(it => it.Name == "date-only-second")
        .Single();
    断言(first.OccurredOn == rows[0].OccurredOn, "DateOnly 批量插入后读取的首行日期不正确。");
    断言(second.OccurredOn == rows[1].OccurredOn, "DateOnly 批量插入后读取的第二行日期不正确。");
}

static DateTime 读取Fastest时间(SqlSugarClient db, string name)
{
    var entity = db.EntityMaintenance.GetEntityInfo<FastSmokeDevice>();
    var sqlBuilder = db.Queryable<FastSmokeDevice>().SqlBuilder;
    var tableName = sqlBuilder.GetTranslationTableName(entity.DbTableName);
    var nameColumn = sqlBuilder.GetTranslationColumnName(entity.Columns.Single(it => it.PropertyName == nameof(FastSmokeDevice.Name)).DbColumnName);
    var timeColumn = sqlBuilder.GetTranslationColumnName(entity.Columns.Single(it => it.PropertyName == nameof(FastSmokeDevice.OccurredAt)).DbColumnName);
    var value = db.Ado.GetScalar(
        "SELECT " + timeColumn + " FROM " + tableName + " WHERE " + nameColumn + " = @name",
        new SugarParameter("@name", name));

    return value switch
    {
        DateTime dateTime => dateTime,
        DateTimeOffset dateTimeOffset => dateTimeOffset.UtcDateTime,
        long unixMilliseconds => DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds).UtcDateTime,
        _ => throw new InvalidOperationException("Fastest 查询未返回可识别的时间值。")
    };
}

static List<SmokeDevice> 创建批量设备()
{
    return
    [
        new SmokeDevice { Name = "identity-boundary-1", Amount = 1, Enabled = true, CreatedAt = DateTime.UtcNow },
        new SmokeDevice { Name = "identity-boundary-2", Amount = 2, Enabled = false, CreatedAt = DateTime.UtcNow }
    ];
}

static void 验证更新与删除(SqlSugarClient db, int identity)
{
    Console.WriteLine("开始：更新与删除");
    var updated = db.Updateable<SmokeDevice>()
        .SetColumns(it => it.Name == "pump-updated")
        .Where(it => it.Id == identity)
        .ExecuteCommand();
    断言(updated == 1, "UPDATE 的受影响行数不是 1。");
    断言(db.Queryable<SmokeDevice>().InSingle(identity).Name == "pump-updated", "UPDATE 后名称未变化。");

    var deleted = db.Deleteable<SmokeDevice>().In(identity).ExecuteCommand();
    断言(deleted == 1, "DELETE 的受影响行数不是 1。");
    断言(db.Queryable<SmokeDevice>().InSingle(identity) == null, "DELETE 后仍能查询到目标行。");
}

static void 验证分页与函数(SqlSugarClient db)
{
    Console.WriteLine("开始：分页与函数");
    var total = 0;
    var page = db.Queryable<SmokeDevice>()
        .Where(it => it.Name == "fan" || it.Name == "json-array")
        .OrderBy(it => it.Id)
        .ToPageList(1, 1, ref total);
    断言(total == 2 && page.Count == 1 && page[0].Name == "fan", "SqlSugar LIMIT/OFFSET 分页结果不正确。");

    var utcNow = db.Ado.GetScalar("SELECT CURRENT_UTC_DATETIME()");
    断言(utcNow is DateTime, "CURRENT_UTC_DATETIME() 未返回 DateTime。");

    var year = db.Ado.GetScalar("SELECT DATE_PART('year', \"CreatedAt\") FROM \"sonnet_smoke_devices\" LIMIT 1");
    断言(Convert.ToInt64(year) == 2026, "DATE_PART 函数结果不正确。");

    var site = db.Ado.GetScalar("SELECT json_value(\"Metadata\", '$.site') FROM \"sonnet_smoke_devices\" WHERE \"Name\" = @name", new SugarParameter("@name", "fan"));
    断言(site == null || site == DBNull.Value, "空 JSON 列的 json_value 应返回 NULL。");
}

static void 验证Json字段访问(SqlSugarClient db)
{
    Console.WriteLine("开始：JSON 静态字段与数组下标");
    var jsonQuery = db.Queryable<SmokeDevice>()
        .Where(it => it.Name == "pump")
        .Select(it => SqlFunc.JsonField(it.Metadata, "site"));
    var site = jsonQuery.Single();
    断言(site == "north", "SqlFunc.JsonField 未正确读取 JSON 标量字段。");

    db.Insertable(new SmokeDevice
    {
        Name = "json-array",
        Amount = 1,
        Enabled = true,
        CreatedAt = DateTime.UtcNow,
        Metadata = "[\"pump\",\"edge\"]"
    }).ExecuteCommand();
    var firstTag = db.Queryable<SmokeDevice>()
        .Where(it => it.Name == "json-array")
        .Select(it => SqlFunc.JsonIndex(it.Metadata, 0))
        .Single();
    断言(firstTag == "pump", "SqlFunc.JsonIndex 未正确读取 JSON 数组下标。");

    必须抛出中文不支持异常(
        "动态 JSON 字段路径",
        () => db.Queryable<SmokeDevice>()
            .Select(it => SqlFunc.JsonField(it.Metadata, it.Name))
            .ToList());

    必须抛出中文不支持异常(
        "跨数据库表引用",
        () => db.Queryable<SmokeDevice>().AS("other_database.sonnet_smoke_devices").ToList());

    验证跨库映射边界(db);
}

static void 验证跨库映射边界(SqlSugarClient db)
{
    Console.WriteLine("开始：映射目标跨库引用边界");
    db.MappingTables ??= new MappingTableList();
    var original = db.MappingTables.FirstOrDefault(it =>
        string.Equals(it.EntityName, nameof(SmokeDevice), StringComparison.OrdinalIgnoreCase));

    db.MappingTables.Add(nameof(SmokeDevice), "other_database.sonnet_smoke_devices");
    try
    {
        必须抛出中文不支持异常(
            "映射目标跨数据库表引用",
            () => db.Queryable<SmokeDevice>().ToList());
        必须抛出中文不支持异常(
            "SqlBuilder 映射目标跨数据库表引用",
            () => db.Queryable<SmokeDevice>().SqlBuilder.GetTranslationTableName(nameof(SmokeDevice)));
    }
    finally
    {
        db.MappingTables.RemoveAll(it =>
            string.Equals(it.EntityName, nameof(SmokeDevice), StringComparison.OrdinalIgnoreCase));
        if (original != null)
        {
            db.MappingTables.Add(original.EntityName, original.DbTableName, original.DbShortTaleName);
        }
    }
}

static void 验证Storageable与批量合并(SqlSugarClient db)
{
    Console.WriteLine("开始：Storageable 与 BulkMerge");
    var existing = db.Queryable<SmokeDevice>().Where(it => it.Name == "pump").Single();
    断言(existing != null, "Storageable 验证需要已存在的 pump 数据。");

    var storageCount = db.Storageable(new List<SmokeDevice>
    {
        new()
        {
            Id = existing.Id,
            Name = "pump-storageable",
            Amount = existing.Amount + 1,
            Enabled = existing.Enabled,
            CreatedAt = existing.CreatedAt
        },
        new()
        {
            Name = "storage-insert",
            Amount = 6.5,
            Enabled = true,
            CreatedAt = existing.CreatedAt
        }
    }).Saveable().ExecuteCommand();
    断言(storageCount == 2, "Storageable 的更新与插入数量不正确。");
    断言(db.Queryable<SmokeDevice>().Any(it => it.Name == "pump-storageable"), "Storageable 未更新已存在的行。");
    断言(db.Queryable<SmokeDevice>().Any(it => it.Name == "storage-insert"), "Storageable 未插入新行。");

    var mergeCount = db.Fastest<SmokeDevice>().BulkMerge(new List<SmokeDevice>
    {
        new()
        {
            Id = existing.Id,
            Name = "pump-merged",
            Amount = existing.Amount + 2,
            Enabled = existing.Enabled,
            CreatedAt = existing.CreatedAt
        },
        new()
        {
            Name = "merge-insert",
            Amount = 7.5,
            Enabled = false,
            CreatedAt = existing.CreatedAt
        }
    });
    断言(mergeCount == 2, "BulkMerge 的更新与插入数量不正确。");
    断言(db.Queryable<SmokeDevice>().Any(it => it.Name == "pump-merged"), "BulkMerge 未更新已存在的行。");
    断言(db.Queryable<SmokeDevice>().Any(it => it.Name == "merge-insert"), "BulkMerge 未插入新行。");

    必须抛出中文不支持异常(
        "Storageable 事务锁",
        () => db.Storageable(new SmokeDevice { Id = existing.Id }).TranLock().ToStorage());
}

static void 验证分表能力边界(SqlSugarClient db)
{
    Console.WriteLine("开始：分表能力边界");
    db.CodeFirst.InitTables<SplitSmokeDevice>();
    必须抛出中文不支持异常(
        "分表查询的 UNION ALL",
        () => db.Queryable<SplitSmokeDevice>().SplitTable().ToList());
}

static void 验证事务(SqlSugarClient db)
{
    Console.WriteLine("开始：关系表事务");
    db.Ado.BeginTran(IsolationLevel.Serializable);
    try
    {
        db.Insertable(new SmokeDevice { Name = "rollback", Amount = 1, Enabled = true, CreatedAt = DateTime.UtcNow }).ExecuteCommand();
        断言(db.Queryable<SmokeDevice>().Where(it => it.Name == "rollback").Count() == 1, "事务内无法读取自己的未提交写入。");
        db.Ado.RollbackTran();
    }
    catch
    {
        db.Ado.RollbackTran();
        throw;
    }

    断言(db.Queryable<SmokeDevice>().Where(it => it.Name == "rollback").Count() == 0, "ROLLBACK 后数据仍然可见。");

    db.Ado.BeginTran();
    try
    {
        db.Insertable(new SmokeDevice { Name = "commit", Amount = 2, Enabled = true, CreatedAt = DateTime.UtcNow }).ExecuteCommand();
        db.Ado.CommitTran();
    }
    catch
    {
        db.Ado.RollbackTran();
        throw;
    }

    断言(db.Queryable<SmokeDevice>().Where(it => it.Name == "commit").Count() == 1, "COMMIT 后数据不可见。");
}

static void 验证提供程序翻译边界(SqlSugarClient db)
{
    Console.WriteLine("开始：提供程序翻译边界");
    var expectedDate = new DateOnly(2026, 9, 19);
    var dateOnlyValue = db.Ado.GetScalar(
        "SELECT @dateOnly",
        new SugarParameter("@dateOnly", expectedDate) { DbType = System.Data.DbType.Date });
    断言(
        是预期日期零点(dateOnlyValue, expectedDate),
        "DateOnly 参数未映射为当天零点的 DateTime。");

    必须抛出中文不支持异常(
        "decimal 参数",
        () => db.Ado.GetScalar("SELECT @amount", new SugarParameter("@amount", 1.25m) { DbType = System.Data.DbType.Decimal }));
    必须抛出中文不支持异常(
        "TimeOnly 参数",
        () => db.Ado.GetScalar("SELECT @timeOnly", new SugarParameter("@timeOnly", new TimeOnly(12, 30)) { DbType = System.Data.DbType.Time }));
    必须抛出中文不支持异常(
        "TimeSpan 参数",
        () => db.Ado.GetScalar("SELECT @timeSpan", new SugarParameter("@timeSpan", TimeSpan.FromMinutes(90))));
    必须抛出中文不支持异常(
        "错误 DateTimeOffset 参数",
        () => db.Ado.GetScalar(
            "SELECT @dateTimeOffset",
            new SugarParameter("@dateTimeOffset", "不是日期", System.Data.DbType.DateTimeOffset)));
    必须抛出中文不支持异常(
        "超范围 UInt64 参数",
        () => db.Ado.GetScalar(
            "SELECT @unsignedValue",
            new SugarParameter("@unsignedValue", ulong.MaxValue, System.Data.DbType.UInt64)));
    必须抛出中文不支持异常(
        "存储过程命令",
        () => db.Ado.UseStoredProcedure().GetScalar("SELECT 1"));
    var falseQuery = db.Queryable<SmokeDevice>()
        .Where(it => it.Name == "fan")
        .Where(it => new[] { false }.Contains(it.Enabled));
    var trueQuery = db.Queryable<SmokeDevice>()
        .Where(it => it.Name == "valve")
        .Where(it => new[] { true }.Contains(it.Enabled));
    var falseCount = falseQuery.Count();
    var trueCount = trueQuery.Count();
    断言(falseCount == 1 && trueCount == 1, "布尔集合 Contains 未正确映射为 TRUE/FALSE。" );
    必须抛出中文不支持异常(
        "SqlSugar With/CTE 公共表表达式",
        () => db.Queryable<SmokeDevice>().With("WITH recent_devices AS (SELECT * FROM sonnet_smoke_devices)").ToList());
    必须抛出中文不支持异常(
        "BETWEEN 条件",
        () => db.Queryable<SmokeDevice>().Where(it => SqlFunc.Between(it.Id, 1, 2)).ToList());
    必须抛出中文不支持异常(
        "UNION ALL 集合运算",
        () => db.UnionAll(
            db.Queryable<SmokeDevice>().Where(it => it.Enabled),
            db.Queryable<SmokeDevice>().Where(it => !it.Enabled)).ToList());
    必须抛出中文不支持异常(
        "RIGHT JOIN 连接",
        () => db.Queryable<SmokeDevice, SmokeDevice>(
            (left, right) => new JoinQueryInfos(JoinType.Right, left.Id == right.Id)).ToList());
    必须抛出中文不支持异常(
        "负数 Skip",
        () => db.Queryable<SmokeDevice>().Skip(-1).ToList());
    必须抛出中文不支持异常(
        "负数 Take",
        () => db.Queryable<SmokeDevice>().Take(-1).ToList());
    必须抛出中文不支持异常(
        "大主键集合删除",
        () => db.Deleteable<SmokeDevice>().In(Enumerable.Range(1, 10000).ToArray()).ToSqlString());

    验证With提示边界(db);
    验证ILike边界(db);
    验证全局无锁与位运算边界(db);
    验证无格式字符串转换(db);
}

static void 验证多表分页边界(SqlSugarClient db)
{
    Console.WriteLine("开始：多表 PartitionBy 边界");
    var multiTableQueryable = db.Queryable<SmokeDevice, SmokeDevice, SmokeDevice, SmokeDevice>(
        (first, second, third, fourth) => new JoinQueryInfos(
            JoinType.Inner,
            first.Id == second.Id,
            JoinType.Inner,
            second.Id == third.Id,
            JoinType.Inner,
            third.Id == fourth.Id));
    ISugarQueryable<SmokeDevice> inheritedQueryable = multiTableQueryable;

    必须抛出中文不支持异常(
        "四表查询继承接口的 PartitionBy",
        () => inheritedQueryable.PartitionBy(device => device.Id));
    必须抛出中文不支持异常(
        "四表查询继承接口的 PartitionBy 字符串重载",
        () => inheritedQueryable.PartitionBy("Id"));
}

static void 验证With提示边界(SqlSugarClient db)
{
    var multiTableQueryable = db.Queryable<SmokeDevice, SmokeDevice>(
        (left, right) => new JoinQueryInfos(JoinType.Inner, left.Id == right.Id));

    必须抛出中文不支持异常(
        "两表 SqlSugar With/CTE 公共表表达式",
        () => multiTableQueryable.With("WITH recent_devices AS (SELECT * FROM sonnet_smoke_devices)"));
    必须抛出中文不支持异常(
        "Insertable With 提示",
        () => db.Insertable(new SmokeDevice()).With(SqlWith.NoLock));
    必须抛出中文不支持异常(
        "Updateable With 提示",
        () => db.Updateable<SmokeDevice>().With(SqlWith.NoLock));
    必须抛出中文不支持异常(
        "Deleteable With 提示",
        () => db.Deleteable<SmokeDevice>().With(SqlWith.NoLock));
}

static void 验证ILike边界(SqlSugarClient db)
{
    var originalSettings = db.CurrentConnectionConfig.MoreSettings;
    var originalEnableILike = originalSettings?.EnableILike;
    if (originalSettings == null)
    {
        db.CurrentConnectionConfig.MoreSettings = new ConnMoreSettings();
    }

    try
    {
        db.CurrentConnectionConfig.MoreSettings!.EnableILike = true;
        必须抛出中文不支持异常(
            "EnableILike Lambda 条件",
            () => db.Queryable<SmokeDevice>().Where(it => it.Name.Contains("fan")).ToList());
        必须抛出中文不支持异常(
            "EnableILike 条件模型",
            () => db.Queryable<SmokeDevice>().Where(ConditionalModel.Create(
                new ConditionalModel
                {
                    FieldName = nameof(SmokeDevice.Name),
                    ConditionalType = ConditionalType.Like,
                    FieldValue = "fan"
                })).ToList());
    }
    finally
    {
        if (originalSettings == null)
        {
            db.CurrentConnectionConfig.MoreSettings = null;
        }
        else
        {
            originalSettings.EnableILike = originalEnableILike.GetValueOrDefault();
        }
    }
}

static void 验证全局无锁与位运算边界(SqlSugarClient db)
{
    var originalSettings = db.CurrentConnectionConfig.MoreSettings;
    var originalNoLock = originalSettings?.IsWithNoLockQuery;
    if (originalSettings == null)
    {
        db.CurrentConnectionConfig.MoreSettings = new ConnMoreSettings();
    }

    try
    {
        db.CurrentConnectionConfig.MoreSettings!.IsWithNoLockQuery = true;
        必须抛出中文不支持异常(
            "全局无锁查询",
            () => db.Queryable<SmokeDevice>().ToList());
    }
    finally
    {
        if (originalSettings == null)
        {
            db.CurrentConnectionConfig.MoreSettings = null;
        }
        else
        {
            originalSettings.IsWithNoLockQuery = originalNoLock.GetValueOrDefault();
        }
    }

    必须抛出中文不支持异常(
        "按位与运算",
        () => db.Queryable<SmokeDevice>().Where(it => (it.Id & 1) == 1).ToList());
    必须抛出中文不支持异常(
        "按位或运算",
        () => db.Queryable<SmokeDevice>().Where(it => (it.Id | 1) == 1).ToList());

    var logicalCount = db.Queryable<SmokeDevice>()
        .Where(it => !it.Enabled && it.Name == "fan")
        .Count();
    断言(logicalCount == 1, "逻辑 && 条件不应被错误识别为按位运算。" );
}

static void 验证无格式字符串转换(SqlSugarClient db)
{
    var device = db.Queryable<SmokeDevice>().Where(it => it.Name == "fan").Single();
    断言(device != null, "无格式 ToString 验证需要已存在的 fan 数据。");

    var count = db.Queryable<SmokeDevice>()
        .Where(it => it.Id.ToString() == device.Id.ToString())
        .Count();
    断言(count == 1, "无格式 ToString 未正确映射为 SonnetDB concat 转换。");
}

static void 验证组合查询(SqlSugarClient db)
{
    Console.WriteLine("开始：组合查询");
    var sqlBuilder = db.Queryable<SmokeDevice>().SqlBuilder;
    断言(
        sqlBuilder.RemoveN("SELECT 'N''x' AS value, N'fan' AS name") == "SELECT 'N''x' AS value, 'fan' AS name",
        "UNION 的字符串前缀处理不应修改字符串内容。" );

    var unionQuery = db.Union(
            db.Queryable<SmokeDevice>().Where(it => it.Name == "fan"),
            db.Queryable<SmokeDevice>().Where(it => it.Name == "valve"));
    var unionRows = unionQuery.OrderBy(it => it.Id).ToList();
    断言(unionRows.Count == 2, "普通 UNION 未返回去重后的两行数据。");

    var innerJoinRows = db.Queryable<SmokeDevice, SmokeDevice>(
            (left, right) => new JoinQueryInfos(JoinType.Inner, left.Id == right.Id))
        .Select((left, right) => new { LeftId = left.Id, RightId = right.Id })
        .ToList();
    断言(innerJoinRows.Count == db.Queryable<SmokeDevice>().Count(), "INNER JOIN 结果行数不正确。");

    var leftJoinRows = db.Queryable<SmokeDevice, SmokeDevice>(
            (left, right) => new JoinQueryInfos(JoinType.Left, left.Id == right.Id + 10000))
        .Select((left, right) => new { LeftId = left.Id, RightName = right.Name })
        .ToList();
    断言(leftJoinRows.Count == db.Queryable<SmokeDevice>().Count() && leftJoinRows.All(it => it.RightName == null), "LEFT JOIN 未保留左侧未匹配行。");

    var pagedJoinRows = db.Queryable<SmokeDevice, SmokeDevice>(
            (left, right) => new JoinQueryInfos(JoinType.Inner, left.Id == right.Id))
        .Select((left, right) => new { LeftId = left.Id, RightId = right.Id })
        .Skip(0)
        .Take(1)
        .ToList();
    断言(pagedJoinRows.Count == 1, "多表分页的自动排序列不应丢失表限定符。");
}

static void 验证方言边界(SqlSugarClient db)
{
    Console.WriteLine("开始：SonnetDB 方言边界");
    using (var reader = db.Ado.GetDataReader("SELECT 1"))
    {
        断言(!reader.NextResult(), "SonnetDB 单结果集读取器不应存在下一个结果集。");
    }

    必须拒绝("多结果集 SQL", () => db.Ado.GetDataTable("SELECT 1; SELECT 2"));
    必须拒绝("CTE 公共表表达式", () => db.Ado.GetDataTable("WITH t AS (SELECT 1) SELECT * FROM t"));
    必须拒绝("RIGHT JOIN 连接", () => db.Ado.GetDataTable("SELECT a.id FROM \"sonnet_smoke_devices\" a RIGHT JOIN \"sonnet_smoke_devices\" b ON a.id = b.id"));
}

static void 验证批量主键列表(SqlSugarClient db)
{
    Console.WriteLine("开始：批量主键列表");
    var identities = db.Insertable(创建批量设备()).ExecuteReturnPkList<int>();
    断言(
        identities.Count == 2 && identities.All(identity => identity > 0),
        "ExecuteReturnPkList 未返回全部批量插入的有效主键。");
}

static void 必须抛出中文不支持异常(string feature, Action action)
{
    try
    {
        action();
    }
    catch (NotSupportedException exception) when (包含中文(exception.Message))
    {
        Console.WriteLine("已确认提供程序限制：" + feature + " 已明确拒绝。");
        return;
    }
    catch (NotSupportedException)
    {
        throw new InvalidOperationException("“" + feature + "”抛出了 NotSupportedException，但异常说明不是中文。");
    }
    catch
    {
        throw new InvalidOperationException("“" + feature + "”必须抛出带中文说明的 NotSupportedException。");
    }

    throw new InvalidOperationException("“" + feature + "”被静默接受，提供程序不得忽略不支持的翻译请求。");
}

static void 必须抛出批量自增键异常(string feature, Action action)
{
    try
    {
        action();
    }
    catch (NotSupportedException exception) when (
        包含中文(exception.Message) &&
        exception.Message.Contains("ExecuteReturnPkList", StringComparison.Ordinal))
    {
        Console.WriteLine("已确认提供程序限制：" + feature + " 已明确拒绝。");
        return;
    }
    catch (NotSupportedException)
    {
        throw new InvalidOperationException("“" + feature + "”抛出了 NotSupportedException，但异常说明不完整或不是中文。");
    }
    catch
    {
        throw new InvalidOperationException("“" + feature + "”必须抛出带中文说明和 ExecuteReturnPkList 指引的 NotSupportedException。");
    }

    throw new InvalidOperationException("“" + feature + "”被静默接受，提供程序不得返回伪造的自增键。");
}

static void 必须拒绝(string feature, Action action)
{
    try
    {
        action();
    }
    catch
    {
        Console.WriteLine("已确认限制：" + feature + " 当前不可用。");
        return;
    }

    throw new InvalidOperationException("预期 SonnetDB 拒绝“" + feature + "”，但 SQL 已执行成功。");
}

static void 断言(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static bool 包含中文(string? value)
{
    return !string.IsNullOrEmpty(value) && value.Any(character => character is >= '\u4e00' and <= '\u9fff');
}

static bool 是预期日期零点(object? value, DateOnly expectedDate)
{
    var expectedUtc = DateTime.SpecifyKind(expectedDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
    return value switch
    {
        DateTime dateTime => dateTime.Date == expectedUtc.Date && dateTime.TimeOfDay == TimeSpan.Zero,
        DateTimeOffset dateTimeOffset => dateTimeOffset.UtcDateTime == expectedUtc,
        long unixMilliseconds => DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds).UtcDateTime == expectedUtc,
        _ => false
    };
}

[SugarTable("sonnet_smoke_devices")]
public sealed class SmokeDevice
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public double Amount { get; set; }

    public bool Enabled { get; set; }

    public DateTime CreatedAt { get; set; }

    [SugarColumn(IsNullable = true, ColumnDataType = "JSON")]
    public string? Metadata { get; set; }

    [SugarColumn(IsNullable = true)]
    public byte[]? Payload { get; set; }
}

[SugarTable("sonnet_default_evolution")]
public sealed class 默认值设备
{
    [SugarColumn(IsPrimaryKey = true)]
    public int Id { get; set; }

    [SugarColumn(DefaultValue = "north")]
    public string Site { get; set; } = string.Empty;
}

[SugarTable("sonnet_default_evolution")]
public sealed class 大小写默认值设备
{
    [SugarColumn(IsPrimaryKey = true)]
    public int Id { get; set; }

    [SugarColumn(DefaultValue = "North")]
    public string Site { get; set; } = string.Empty;
}

[SugarTable("sonnet_default_evolution")]
public sealed class 移除默认值设备
{
    [SugarColumn(IsPrimaryKey = true)]
    public int Id { get; set; }

    public string Site { get; set; } = string.Empty;
}

[SugarTable("sonnet_string_numeric_default")]
public sealed class 字符串数字默认值设备
{
    [SugarColumn(IsPrimaryKey = true)]
    public int Id { get; set; }

    [SugarColumn(DefaultValue = "1")]
    public string Text { get; set; } = string.Empty;
}

[SugarTable("sonnet_unsigned_identity_devices")]
public sealed class UnsignedIdentitySmokeDevice
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public uint Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

[SugarTable("sonnet_long_identity_devices")]
public sealed class LongIdentitySmokeDevice
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

[SugarTable("SonnetMixedCase")]
public sealed class 大小写设备
{
    [SugarColumn(IsPrimaryKey = true)]
    public int RecordId { get; set; }

    public string DisplayName { get; set; } = string.Empty;
}

[SugarTable("sonnet_special_parameter_devices")]
public sealed class 特殊列名设备
{
    [SugarColumn(IsPrimaryKey = true)]
    public int Id { get; set; }

    [SugarColumn(ColumnName = "order-id")]
    public string OrderId { get; set; } = string.Empty;
}

[SugarTable("sonnet_fast_smoke_devices")]
public sealed class FastSmokeDevice
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public double Amount { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public string ClockValue { get; set; } = string.Empty;
}

[SugarTable("sonnet_fast_smoke_devices")]
public sealed class FastDecimalDevice
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public string ClockValue { get; set; } = string.Empty;
}

[SugarTable("sonnet_fast_smoke_devices")]
public sealed class FastTimeOnlyDevice
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public double Amount { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public TimeOnly ClockValue { get; set; }
}

[SugarTable("sonnet_date_only_smoke_devices")]
public sealed class DateOnlySmokeDevice
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateOnly OccurredOn { get; set; }
}

[SugarTable("sonnet_split_smoke_devices_{year}_{month}_{day}")]
[SplitTable(SplitType.Day)]
public sealed class SplitSmokeDevice
{
    [SugarColumn(IsPrimaryKey = true)]
    public int Id { get; set; }

    [SplitField]
    public DateTime OccurredAt { get; set; }

    public string Name { get; set; } = string.Empty;
}
