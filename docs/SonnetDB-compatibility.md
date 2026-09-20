# SonnetDB 与 SqlSugar 兼容性矩阵

本文记录 SqlSugar `SonnetDB` 提供程序的方言边界，以及需要由 SonnetDB 补齐的通用 SQL 能力。它不是 SonnetDB 的完整 SQL 手册；以 SonnetDB 当前源码、测试和 `3.1.0` 包目标为依据，重点覆盖 SqlSugar 会自动生成的关系型 SQL。

更新时间：2026-09-20

## 结论

SonnetDB 已具备 SqlSugar 关系查询的核心路径：双引号标识符、参数、筛选、排序、分页、`DISTINCT`、普通 `UNION`、内连接/左连接、子查询、聚合、`GROUP BY`、`HAVING`、`CASE`、`IN`、`EXISTS` 以及基本 DML。

提供程序只能生成 SonnetDB 已确认的方言。无法保真翻译的 SqlSugar 特性必须在生成 SQL 前抛出 `NotSupportedException`，不能静默降级为语义不同的 SQL，也不能继续复用 PostgreSQL 方言。

## 运行时与发布边界

- `SqlSugar.SonnetDBCore` 和 SonnetDB 3.1.0 驱动目前仅提供 `net10.0` 资产，使用方需要 .NET 10 或更高版本。
- SqlSugar 通过旁加载 `SqlSugar.SonnetDBCore.dll` 创建提供程序类型；请在普通发布输出中保留该 DLL 与 `SqlSugar.dll`。NativeAOT、激进裁剪和 single-file 捆绑发布不在当前验证范围内，除非应用自行预加载程序集并配置相应保留规则。

## 判定标准与证据

状态含义：

| 状态 | 含义 |
| --- | --- |
| 已确认 | SonnetDB 源码存在对应解析器/执行器路径，并有相关测试或聚焦测试覆盖。 |
| 可映射 | SonnetDB 有等价 SQL；SqlSugar 提供程序应生成表中列出的目标 SQL。仍需提供程序端到端测试。 |
| 有限制 | 引擎支持主路径，但存在参数、类型或语义约束。 |
| 显式拒绝 | 提供程序不应生成该语法，应在翻译阶段报错。 |
| 已提 issue | SonnetDB 缺少通用 SQL 能力，已发布条目见 [SonnetDB-issues.md](SonnetDB-issues.md)。 |

已核对的 SonnetDB 源码位置均相对于 SonnetDB 仓库根目录：

- `src/SonnetDB.Core/Query/Functions/FunctionRegistry.cs`：内置聚合和标量函数注册。
- `src/SonnetDB.Core/Query/Functions/SqlDateTimeFunctions.cs`：日期时间函数的参数和返回语义。
- `src/SonnetDB.Core/Sql/SqlLexer.cs`、`SqlParser.cs`：已接受的关键字、连接、`UNION`、分页、子查询和表达式语法。
- `src/SonnetDB.Core/Sql/Execution/SqlExecutor.cs`、`RelationalSelectExecutor.cs`：关系查询、`DISTINCT`、`UNION`、连接、聚合和子查询执行。
- `src/SonnetDB.Core/Sql/Execution/TableSqlExecutor.cs`：单表表达式验证及 `json_value` 的固定 JSON path 约束。

本次已运行的聚焦验证：

```powershell
dotnet test tests/SonnetDB.Core.Tests/SonnetDB.Core.Tests.csproj --no-restore --filter "FullyQualifiedName~SqlParserTests|FullyQualifiedName~SqlExpressionExecutionTests|FullyQualifiedName~SqlExecutorSelectTests|FullyQualifiedName~SqlParameterizedQueryTests|FullyQualifiedName~SqlRegexContractTests|FullyQualifiedName~RelationalJoinAlgorithmTests|FullyQualifiedName~TableIndexUnionTests" --logger "console;verbosity=minimal"
```

结果：`227` 通过，`0` 失败。

还运行了：

```powershell
dotnet test tests/SonnetDB.IoTSharpCompat.Tests/SonnetDB.IoTSharpCompat.Tests.csproj --no-restore --filter "Category!=Documentation" --logger "console;verbosity=minimal"
```

结果：`1` 通过，`0` 失败。该项目不是 SqlSugar 提供程序的集成测试，不能据此宣称 SqlSugar 端到端已验证。

## 已确认的查询能力

| SqlSugar 使用面 | SonnetDB 目标 SQL / 规则 | 状态 | 注意事项 |
| --- | --- | --- | --- |
| 标识符与别名 | `"Device"`、`"d"."Id"` | 可映射 | 双引号标识符保留原始大小写；提供程序不应默认转小写。 |
| 参数 | `@p`、`:p` | 已确认 | SqlSugar 提供程序会统一绑定命名参数；原始 SonnetDB SQL 的 `?` 位置占位符不属于 SqlSugar `SugarParameter` 兼容接口。分页参数同样使用命名参数。 |
| 基础筛选 | `= <> != < <= > >=`、`AND`、`OR`、`NOT`、`IS [NOT] NULL` | 已确认 | 保留 SQL 三值逻辑。 |
| 条件表达式 | `CASE WHEN ... THEN ... ELSE ... END` | 已确认 | 用于 `SqlFunc.IIF`。 |
| 集合与子查询 | `IN`、`NOT IN`、`EXISTS`、标量/派生表子查询 | 已确认 | 关系执行器有非相关子查询缓存和相关子查询路径；复杂 DML 子查询仍有局部限制。 |
| 模式匹配 | `LIKE`、`NOT LIKE`、`regexp_like(value, pattern[, flags])` | 已确认 | `Contains`、`StartsWith`、`EndsWith` 应使用 `LIKE concat(...)`。 |
| 排序 | `ORDER BY expr ASC|DESC` | 已确认 | 分页没有显式排序时，提供程序应按首个投影列补稳定排序，不能生成 PostgreSQL `NOW()`。 |
| 分页 | `LIMIT n OFFSET m`、`OFFSET n`、`OFFSET n ROWS FETCH NEXT m ROWS ONLY` | 已确认 | `n`、`m` 必须为非负 `Int32`；提供程序会拒绝负数 `Skip`/`Take`，Skip-only 不能生成 `long.MaxValue`。 |
| 去重 | `SELECT DISTINCT ...` | 已确认 | 普通 `DISTINCT` 可以使用；聚合参数中的 `DISTINCT` 见后文。 |
| 集合并集 | `SELECT ... UNION SELECT ...` | 已确认 | `UNION` 会去重；不支持 `UNION ALL`。作为派生表时必须使用 `FROM (SELECT ... UNION SELECT ...) 别名`，不能给每个分支再包一层括号；分支内不支持 `ORDER BY`、`LIMIT`、`OFFSET`，只能在复合查询尾部统一排序和分页。 |
| 连接 | `JOIN`、`INNER JOIN`、`LEFT JOIN`、`LEFT OUTER JOIN` | 已确认 | 解析器和关系执行器只定义 Inner/Left 两种 JoinKind；多关系表连接有测试。 |
| 分组和聚合 | `COUNT`、`SUM`、`MIN`、`MAX`、`AVG`、`GROUP BY`、`HAVING` | 已确认 | 关系查询会进入 `RelationalSelectExecutor`；支持分组列、聚合表达式和 HAVING。 |
| 布尔值 | `TRUE`、`FALSE` | 可映射 | 提供程序应输出布尔字面量，不使用 `1` / `0` 作为布尔常量；布尔集合 `Contains` 会映射为 `IN (TRUE, FALSE)`。 |
| 数值函数 | `abs`、`round`、`sqrt`、`log`、`coalesce`、`concat`、`lower`、`upper`、`%` | 可映射 | `round` 接受一或两个参数，`concat` 将 NULL 视为空字符串。 |
| 无格式字符串转换 | `ToString`、`ToVarchar` 映射为 `concat('', value)` | 有限制 | 仅适用于 SonnetDB 已支持的标量；`concat` 使用不变区域性文本表示，NULL 为空字符串，不保证等同 CLR 当前区域性、JSON/BLOB 或自定义格式的转换结果。 |
| 当前时间 | `current_datetime()`、`current_utc_datetime()` | 可映射 | 提供程序统一使用 `current_utc_datetime()`，避免遗留 `current_timestamp`/`NOW()`。 |
| 日期提取 | `date_only(value)`、`date_part('year', value)` | 可映射 | `date_part` 支持 year、quarter、month、day、day_of_year、day_of_week、hour、minute、second、millisecond、microsecond、nanosecond。 |
| 日期加法 | `date_add(value, amount, 'day')` | 可映射 | 支持 year、month、day、hour、minute、second、millisecond、microsecond、tick；year/month/tick 的 amount 必须为整数。 |
| Unix 时间 | `to_unix_seconds(value)`、`to_unix_milliseconds(value)`、`to_datetime(value)` | 可映射 | 用于明确可保真的日期转换；不能当作任意 SQL 类型 CAST。 |
| JSON 标量字段 | `json_value("Metadata", '$.site')` | 有限制 | 第二个参数必须是 JSON path 字符串字面量；SqlSugar 仅应映射静态路径的 `JsonField`。 |
| 基本关系 DML | `INSERT`、`UPDATE`、`DELETE`、`RETURNING` | 已确认 | SonnetDB 关系表支持这些语句；仍需补 SqlSugar Insertable/Updateable/Deleteable 的端到端用例。 |

## 可直接作为提供程序回归用例的 SQL

```sql
SELECT DISTINCT "d"."Name", "d"."Enabled"
FROM "Devices" AS "d"
WHERE "d"."Enabled" = TRUE
  AND "d"."Name" LIKE concat('%', @name, '%')
ORDER BY "d"."Name"
LIMIT 20 OFFSET 0;
```

```sql
SELECT "Region", COUNT(*) AS "Count", SUM("Amount") AS "Total"
FROM "Orders"
GROUP BY "Region"
HAVING SUM("Amount") >= 100
ORDER BY "Region";
```

```sql
SELECT date_part('year', "CreatedAt") AS "Year",
       date_add("CreatedAt", 7, 'day') AS "NextWeek",
       to_unix_milliseconds("CreatedAt") AS "CreatedAtMs"
FROM "Devices"
WHERE "CreatedAt" <= current_utc_datetime();
```

```sql
SELECT json_value("Metadata", '$.site') AS "Site"
FROM "Devices"
WHERE json_value("Metadata", '$.enabled') = TRUE;
```

## 必须显式拒绝的 SqlSugar 特性

这些特性不能依赖基类或 PostgreSQL/DuckDB 遗留实现继续输出 SQL。对应的 SonnetDB 能力缺口和已发布 issue 见下表。

| SqlSugar 特性或遗留 SQL | 原因 | 提供程序行为 | 后续 |
| --- | --- | --- | --- |
| Queryable、Insertable、Updateable、Deleteable 的 `With(...)`，以及 CTE `WITH name AS (...)` | SonnetDB 的 `WITH` 仅用于特定 DDL 选项，SELECT 解析器没有 CTE 入口，也没有与 SqlSugar 表提示或锁提示等价的语义。 | 所有公开 `With` 路径均显式拒绝；内部 `SqlWith.Null` 哨兵除外。 | [SDB-SQL-01](SonnetDB-issues.md#sdb-sql-01-支持非递归-cte) |
| `PartitionBy(...)`、`RowNumber`、`RowCount`、`RowSum`、`RowAvg`、`RowMin`、`RowMax`、`OVER(...)` | SonnetDB 有时序原生窗口函数，但没有 ANSI `OVER` 语法或 `ROW_NUMBER`。 | 显式拒绝，不把 PartitionBy 误改为 `GROUP BY`。 | [SDB-SQL-02](SonnetDB-issues.md#sdb-sql-02-支持关系表-ansi-窗口函数-over) |
| `ToInt32`、`ToInt64`、`ToSingle`、`ToDouble`、`ToGuid`、`ToDecimal`、`ToBool` 及格式化 `ToString` | 未确认 `CAST(expr AS type)` 或等价的全套显式转换语义。 | 显式拒绝；仅在已有 SonnetDB 日期函数可精确保真时使用它们。 | [SDB-SQL-03](SonnetDB-issues.md#sdb-sql-03-支持标准-cast-和明确的类型转换规则) |
| `Trim`、`Replace`、`Substring`、`Length`、`CharIndex`、`StringJoin`、格式化 | 当前内置标量注册表没有这些 SQLSugar 所需函数。 | 显式拒绝。 | [SDB-SQL-04](SonnetDB-issues.md#sdb-sql-04-补齐关系表常用字符串函数) |
| `DateDiff`、自定义日期格式、`WeekOfYear`、不能精确等价的 `DateIsSame*` | `date_part`/`date_add` 不能定义所有 SqlSugar API 的日历差值和格式化语义。 | 显式拒绝。 | [SDB-SQL-05](SonnetDB-issues.md#sdb-sql-05-提供明确的-date_diff-和日期格式化能力) |
| `COUNT(DISTINCT x)`、`SUM(DISTINCT x)`、`AVG(DISTINCT x)` | SELECT `DISTINCT` 可用，但聚合函数的 `DISTINCT` 参数不是当前解析器/执行器的关系聚合语法。 | 显式拒绝。 | [SDB-SQL-06](SonnetDB-issues.md#sdb-sql-06-支持关系查询的聚合-distinct) |
| RIGHT/FULL/CROSS JOIN | 解析器仅接受 Inner/Left，关系执行器 JoinKind 也仅有两种。 | 显式拒绝。 | [SDB-SQL-07](SonnetDB-issues.md#sdb-sql-07-扩展标准连接类型) |
| `UNION ALL`、`INTERSECT`、`EXCEPT` | 当前只有去重语义的 `UNION`。 | 显式拒绝。 | [SDB-SQL-08](SonnetDB-issues.md#sdb-sql-08-扩展集合运算) |
| `BETWEEN`、`ILIKE`、`MoreSettings.EnableILike` | 词法分析器/解析器没有对应条件运算符；`ILIKE` 不能安全改写为 `lower(...) LIKE lower(...)` 而不先定义 Unicode、索引和转义语义。 | 显式拒绝；启用 `EnableILike` 时 Lambda 与条件模型翻译入口均拒绝。 | [SDB-SQL-09](SonnetDB-issues.md#sdb-sql-09-补齐常用谓词-between-和-ilike) |
| PostgreSQL `::`、`INTERVAL`、`to_char`、`date_trunc`、`strpos`、JSONB `->`/`->>`/`@>` | 不是 SonnetDB 方言。 | 显式拒绝；分别使用已确认的日期、`concat`、`json_value` 能力。 | [SDB-SQL-03](SonnetDB-issues.md#sdb-sql-03-支持标准-cast-和明确的类型转换规则)、[SDB-SQL-04](SonnetDB-issues.md#sdb-sql-04-补齐关系表常用字符串函数)、[SDB-SQL-10](SonnetDB-issues.md#sdb-sql-10-扩展-json-标量与数组查询能力) |
| 高级 JSON：数组长度、数组/对象包含、动态 path | 仅确认静态 path 的 `json_value`。 | 显式拒绝。 | [SDB-SQL-10](SonnetDB-issues.md#sdb-sql-10-扩展-json-标量与数组查询能力) |
| 整数按位 `&`、`|` 以及 `SqlFunc.BitwiseAnd`/`BitwiseInclusiveOR` | SonnetDB 当前词法器和关系表达式 AST 没有按位运算符。 | 显式拒绝；不能等到驱动层解析失败。 | [SDB-SQL-13](SonnetDB-issues.md#sdb-sql-13-支持关系-sql-整数按位与或运算) |
| `decimal`、`Currency`、`VarNumeric` | 关系表仅有 `FLOAT`，转换为 `double` 会无提示丢失精度。 | 显式拒绝。 | [SDB-SQL-11](SonnetDB-issues.md#sdb-sql-11-支持关系表精确-decimalnumeric) |
| `TimeOnly`、`TimeSpan`、`TIME` | 关系表没有 `TIME` 类型，不能借用 `DATETIME` 而改变仅时间值的语义。 | 显式拒绝；`DateOnly` 映射为当日零点 `DATETIME`。 | [SDB-SQL-12](SonnetDB-issues.md#sdb-sql-12-支持关系表-time-类型) |
| `TranLock`、表提示 `WITH(NOLOCK)`、`FOR UPDATE`、`MoreSettings.IsWithNoLockQuery` | SonnetDB 未暴露与 SqlSugar 锁提示一一等价的行级锁 SQL。 | 显式拒绝，不忽略调用。 | 先作为产品语义讨论，不应伪造兼容。 |
| 跨数据库表引用（导航映射中的 `database.table`） | SonnetDB ADO 连接只绑定当前数据库，没有跨数据库查询语义。 | 显式拒绝，不拼接 `database.table`。 | 提供程序限制；需要独立的跨库连接或 SonnetDB 多库语义后再评估。 |
| `Deleteable.In(...)` 达到或超过 10,000 个主键（`BigDataInValues`） | SqlSugar 基类会拆成以分号连接的多条 `DELETE`，而 SonnetDB 不支持多语句。 | 显式拒绝；调用方需自行分批删除。 | 提供程序限制。 |
| 多表 UPDATE/DELETE | SQL Server/PostgreSQL 生成形式与 SonnetDB 已确认 DML 面不匹配。 | 显式拒绝。 | 需要独立场景和事务语义后再评估。 |

## CodeFirst、DbFirst 与维护接口

SonnetDB 提供程序已有基于 ADO/`information_schema` 元数据的表、列、索引和视图读取路径，以及关系表 DDL 的 CodeFirst 路径；它不等于完整的 SqlSugar `DbMaintenance` 方言兼容。

当前应当明确保留的限制：

- 不支持列/表备注、备份、临时表、`CREATE TABLE AS SELECT`、SqlSugar INCLUDE 索引列。
- 现有表不应通过 CodeFirst 改写主键或 `AUTO_INCREMENT` 定义。
- SonnetDB 关系表 DDL 必须有主键；类型范围是 `INT`、`FLOAT`、`BOOL`、`STRING`、`DATETIME`、`BLOB`、`JSON`。
- `decimal`/`NUMERIC`、`TimeOnly`/`TIME` 不会被降级映射；提供程序会在 CodeFirst 或参数绑定阶段明确拒绝。
- CodeFirst 支持普通列默认值的新增、修改和删除；删除默认值使用 SonnetDB 原生 `ALTER COLUMN ... DROP DEFAULT`。
- `DbMaintenance.SetAutoIncrementInitialValue(...)` 没有等价语义；SonnetDB 自增列从 `1` 开始，提供程序会抛出中文 `NotSupportedException`。如需推进高水位，请显式插入整数值。
- `DropView`、`DropProc` 和 `DropConstraint` 已映射到 SonnetDB 原生 DDL，并会对表名、对象名进行标识符转义；用户定义函数对象不存在，因此 `DropFunction`、`GetFuncList` 明确拒绝。
- `GetProcList`、`IsAnyProcedure` 和 `GetTriggerNames` 使用 SonnetDB 的 `SHOW` 管理语法；`GetViewInfoList` 查询 `information_schema.views` 返回视图名称和 `DbObjectType.View`，DbFirst 仍不会自动生成视图类。
- 这些属于提供程序 API 覆盖和 SonnetDB DDL 产品边界，需用独立 CodeFirst/DbFirst 集成测试验证，不能由查询测试替代。

## ADO 与事务边界

- SonnetDB 提供程序仅支持普通 SQL 文本命令，不支持存储过程或表直接命令。底层 `SndbCommand` 的表直接批量入库路径不是 SqlSugar 的 `CommandType` 兼容接口；虽然 SonnetDB 具有 SQL 过程能力，提供程序也不能把 ADO.NET 的存储过程命令映射为过程调用。
- 不支持输出、输入输出、返回值参数，也不支持多语句或多结果集；`SndbDataReader.NextResult()` 始终返回 `false`。
- 关系表 DML 可使用轻量事务；DDL、临时表、SqlSugar 临时表批量更新和带 JOIN 的批量 UPDATE 不能放入该兼容路径。
- `Fastest` 的逐行批量写入会加入当前 `SndbTransaction`，但不提供数据库原生批量导入协议。
- `BulkMerge` 在 SonnetDB 上回退为分页面向 `Storageable` 的匹配、逐行更新和插入；未由调用方包裹事务时，中途失败可能保留已完成页面或行的写入。需要全量原子性时，请显式开启事务并在成功后提交。
- `MoreSettings.IsWithNoLockQuery` 全局无锁查询配置会被显式拒绝；SonnetDB 不会静默假装提供 `NOLOCK` 语义。

## 端到端验收清单

合并提供程序前至少新增并通过以下 SqlSugar 侧测试：

1. 建表、插入、更新、删除、参数化查询与 `RETURNING`（若对应 API 使用）。
2. 映射列和映射表保留大小写，含别名、连接、子查询。
3. `Skip/Take` 的 take-only、skip-only、skip/take、零值、`Int32.MaxValue` 边界和无显式排序场景。
4. `DISTINCT`、普通 `UNION`、`GROUP BY/HAVING`、INNER/LEFT JOIN、`IN`、`EXISTS`。
5. `IIF`、空值、字符串 LIKE、数值、日期、静态 JSON path 的翻译结果和真实执行结果。
6. 每一项“显式拒绝”均断言翻译期抛出异常，特别是 CTE、窗口函数、PG cast/JSON 运算符和锁提示。
7. CodeFirst/DbFirst 最小闭环：创建、读取 schema、添加普通列、创建/删除普通索引，以及已知不支持操作的异常信息。

原始 SQL（`SqlQueryable`、`Ado.SqlQuery` 等）由调用方负责遵循 SonnetDB 方言；提供程序无法也不应尝试把任意手写 PostgreSQL/SQL Server SQL 重写为 SonnetDB SQL。
