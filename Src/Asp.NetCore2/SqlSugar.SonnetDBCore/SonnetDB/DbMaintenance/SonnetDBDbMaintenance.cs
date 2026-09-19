using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using SonnetDB.Data;

namespace SqlSugar.SonnetDB
{
    /// <summary>
    /// SonnetDB 通过 <see cref="SndbConnection.GetSchema"/> 和 information_schema 提供关系对象元数据。
    /// </summary>
    public class SonnetDBDbMaintenance : DbMaintenanceProvider
    {
        #region 必需的 SQL 模板

        protected override string GetDataBaseSql => throw new NotSupportedException("SonnetDB 不支持此数据库维护 SQL 模板。");

        protected override string GetColumnInfosByTableNameSql => throw new NotSupportedException("SonnetDB 不支持此数据库维护 SQL 模板。");

        protected override string GetTableInfoListSql => throw new NotSupportedException("SonnetDB 不支持此数据库维护 SQL 模板。");

        protected override string GetViewInfoListSql => "SELECT table_name AS Name FROM information_schema.views";

        protected override string CreateDataBaseSql => throw new NotSupportedException("SonnetDB 不支持通过 SqlSugar 创建数据库。");

        protected override string AddPrimaryKeySql => throw new NotSupportedException("SonnetDB 不支持为已有表新增主键。");

        protected override string AddColumnToTableSql => "ALTER TABLE {0} ADD COLUMN {1} {2} {3} {4}";

        protected override string AlterColumnToTableSql => "ALTER TABLE {0} ALTER COLUMN {1} {2} {4}";

        protected override string BackupDataBaseSql => throw new NotSupportedException("SonnetDB 不支持通过 SqlSugar 备份数据库。");

        protected override string CreateTableSql => "CREATE TABLE {0} (\r\n{1}\r\n)";

        protected override string CreateTableColumn => "{0} {1} {2} {3} {4}";

        protected override string BackupTableSql => throw new NotSupportedException("SonnetDB 不支持通过 SqlSugar 备份表。");

        protected override string TruncateTableSql => "TRUNCATE TABLE {0}";

        protected override string DropTableSql => "DROP TABLE {0}";

        protected override string DropColumnToTableSql => "ALTER TABLE {0} DROP COLUMN {1}";

        protected override string DropConstraintSql => "ALTER TABLE {0} DROP CONSTRAINT {1}";

        protected override string RenameColumnSql => "ALTER TABLE {0} RENAME COLUMN {1} TO {2}";

        protected override string RenameTableSql => "ALTER TABLE {0} RENAME TO {1}";

        protected override string CreateIndexSql => "CREATE {3} INDEX {2} ON {0} ({1})";

        protected override string IsAnyIndexSql => throw new NotSupportedException("SonnetDB 不支持此数据库维护 SQL 模板。");

        protected override string AddDefaultValueSql => "ALTER TABLE {0} ALTER COLUMN {1} SET DEFAULT {2}";

        protected override string AddColumnRemarkSql => throw new NotSupportedException("SonnetDB 不支持列备注。");

        protected override string DeleteColumnRemarkSql => throw new NotSupportedException("SonnetDB 不支持列备注。");

        protected override string IsAnyColumnRemarkSql => throw new NotSupportedException("SonnetDB 不支持列备注。");

        protected override string AddTableRemarkSql => throw new NotSupportedException("SonnetDB 不支持表备注。");

        protected override string DeleteTableRemarkSql => throw new NotSupportedException("SonnetDB 不支持表备注。");

        protected override string IsAnyTableRemarkSql => throw new NotSupportedException("SonnetDB 不支持表备注。");

        protected override string CheckSystemTablePermissionsSql => "SELECT 1";

        protected override string CreateTableNull => string.Empty;

        protected override string CreateTableNotNull => "NOT NULL";

        protected override string CreateTablePirmaryKey => "PRIMARY KEY";

        protected override string CreateTableIdentity => "AUTO_INCREMENT";

        #endregion

        #region 元数据

        public override List<string> GetDataBaseList()
        {
            var database = GetConnection().Database;
            return string.IsNullOrWhiteSpace(database)
                ? new List<string>()
                : new List<string> { database };
        }

        public override List<string> GetDataBaseList(SqlSugarClient db)
        {
            var database = db.Ado.Connection.Database;
            return string.IsNullOrWhiteSpace(database)
                ? new List<string>()
                : new List<string> { database };
        }

        public override List<DbTableInfo> GetTableInfoList(bool isCache = true)
        {
            var metadata = GetSchema("Tables");
            var result = new List<DbTableInfo>();
            foreach (DataRow row in metadata.Rows)
            {
                var type = GetString(row, "TABLE_TYPE");
                if (!string.IsNullOrEmpty(type) &&
                    !type.Equals("BASE TABLE", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var name = GetString(row, "TABLE_NAME");
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                result.Add(new DbTableInfo
                {
                    Name = name,
                    DbObjectType = DbObjectType.Table
                });
            }

            return result.OrderBy(it => it.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public override List<DbTableInfo> GetViewInfoList(bool isCache = true)
        {
            return ReadObjectNames("SELECT table_name FROM information_schema.views")
                .Select(name => new DbTableInfo
                {
                    Name = name,
                    DbObjectType = DbObjectType.View
                })
                .ToList();
        }

        public override List<DbColumnInfo> GetColumnInfosByTableName(string tableName, bool isCache = true)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                return new List<DbColumnInfo>();
            }

            SonnetDBIdentifier.ThrowIfCrossDatabaseReference(tableName);
            var normalizedTableName = NormalizeIdentifier(tableName);
            var metadata = GetSchema("Columns", normalizedTableName);
            var result = new List<DbColumnInfo>();
            foreach (DataRow row in metadata.Rows)
            {
                var columnName = GetString(row, "COLUMN_NAME");
                if (string.IsNullOrEmpty(columnName))
                {
                    continue;
                }

                result.Add(new DbColumnInfo
                {
                    TableId = GetInt(row, "ORDINAL_POSITION") - 1,
                    TableName = GetString(row, "TABLE_NAME") ?? normalizedTableName,
                    DbColumnName = columnName,
                    DataType = GetString(row, "DATA_TYPE") ?? string.Empty,
                    DefaultValue = GetString(row, "COLUMN_DEFAULT"),
                    IsNullable = GetBool(row, "IS_NULLABLE"),
                    IsPrimarykey = GetBool(row, "IS_PRIMARY_KEY"),
                    IsIdentity = GetBool(row, "IS_AUTO_INCREMENT"),
                    // STRING、JSON 与 BLOB 没有长度上限，不能伪造长度导致代码优先重复变更。
                    Length = 0,
                    DecimalDigits = 0,
                    Scale = 0
                });
            }

            return result
                .OrderBy(it => it.TableId)
                .ThenBy(it => it.DbColumnName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public override List<string> GetIndexList(string tableName)
        {
            var metadata = GetSchema("Indexes", NormalizeIdentifier(tableName));
            return metadata.Rows.Cast<DataRow>()
                .Select(row => GetString(row, "INDEX_NAME"))
                .Where(name => !string.IsNullOrEmpty(name))
                .Select(name => name!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public override List<string> GetDbTypes()
        {
            return new List<string>
            {
                "INT", "FLOAT", "BOOL", "STRING", "DATETIME", "BLOB", "JSON"
            };
        }

        public override List<string> GetProcList()
        {
            return ReadObjectNames("SHOW PROCEDURES");
        }

        public override List<string> GetProcList(string dbName)
        {
            if (!string.IsNullOrWhiteSpace(dbName) &&
                !string.Equals(dbName, GetConnection().Database, StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException("SonnetDB 只有当前连接数据库，不支持按其他数据库名称查询过程列表。");
            }

            return GetProcList();
        }

        public override List<string> GetFuncList()
        {
            throw new NotSupportedException("SonnetDB 不支持用户定义函数对象，不能通过 SqlSugar 查询函数列表。");
        }

        public override List<string> GetTriggerNames(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException("查询 SonnetDB 触发器时必须提供表名。", nameof(tableName));
            }

            return ReadObjectNames(
                "SHOW TRIGGERS ON " + SqlBuilder.GetTranslationTableName(tableName));
        }

        public override bool IsAnySystemTablePermissions()
        {
            try
            {
                _ = GetSchema("Tables");
                return true;
            }
            catch
            {
                return false;
            }
        }

        public override bool IsAnyIndex(string indexName)
        {
            var normalizedIndexName = NormalizeIdentifier(indexName);
            var metadata = GetSchema("Indexes");
            return metadata.Rows.Cast<DataRow>().Any(row => string.Equals(
                GetString(row, "INDEX_NAME"),
                normalizedIndexName,
                StringComparison.OrdinalIgnoreCase));
        }

        public override bool IsAnyConstraint(string constraintName)
        {
            throw new NotSupportedException(
                "SonnetDB ADO 元数据未提供按名称查询约束的能力。请使用架构迁移 SQL 或检查表定义。");
        }

        public override bool IsAnyProcedure(string procName)
        {
            if (string.IsNullOrWhiteSpace(procName))
            {
                throw new ArgumentException("过程名称不能为空。", nameof(procName));
            }

            return GetProcList().Any(name =>
                string.Equals(name, NormalizeIdentifier(procName), StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        #region DDL

        public override bool CreateTable(string tableName, List<DbColumnInfo> columns, bool isCreatePrimaryKey = true)
        {
            if (columns == null || columns.Count == 0)
            {
                throw new ArgumentException("SonnetDB 建表至少需要一个列。", nameof(columns));
            }

            var primaryKeys = columns.Where(it => it.IsPrimarykey).ToList();
            if (!isCreatePrimaryKey || primaryKeys.Count == 0)
            {
                throw new NotSupportedException(
                    "SonnetDB 关系表需要显式主键。请将一个或多个实体列标记为 IsPrimaryKey = true。");
            }

            var identityColumns = columns.Where(it => it.IsIdentity).ToList();
            if (identityColumns.Count > 1)
            {
                throw new NotSupportedException("SonnetDB 每个关系表最多支持一个自增列。");
            }

            var definitions = columns.Select(BuildColumnDefinition).ToList();
            definitions.Add("PRIMARY KEY (" + string.Join(", ", primaryKeys.Select(it =>
                SqlBuilder.GetTranslationColumnName(it.DbColumnName))) + ")");

            var sql = string.Format(
                CreateTableSql,
                SqlBuilder.GetTranslationTableName(tableName),
                string.Join(",\r\n", definitions));
            Context.Ado.ExecuteCommand(sql);
            return true;
        }

        public override bool AddColumn(string tableName, DbColumnInfo columnInfo)
        {
            if (columnInfo == null)
            {
                throw new ArgumentNullException(nameof(columnInfo), "列定义不能为空。");
            }

            if (columnInfo.IsPrimarykey || columnInfo.IsIdentity)
            {
                throw new NotSupportedException(
                    "SonnetDB 不能通过 SqlSugar 代码优先功能为已有表新增主键或自增列。请创建替代表并迁移数据。");
            }

            var sql = "ALTER TABLE " + SqlBuilder.GetTranslationTableName(tableName) +
                      " ADD COLUMN " + BuildColumnDefinition(columnInfo);
            Context.Ado.ExecuteCommand(sql);
            return true;
        }

        public override bool UpdateColumn(string tableName, DbColumnInfo columnInfo)
        {
            if (columnInfo == null)
            {
                throw new ArgumentNullException(nameof(columnInfo), "列定义不能为空。");
            }

            if (columnInfo.IsPrimarykey || columnInfo.IsIdentity)
            {
                throw new NotSupportedException(
                    "SonnetDB 不允许 SqlSugar 代码优先功能修改主键或自增列。");
            }

            var nullability = columnInfo.IsNullable ? "NULL" : "NOT NULL";
            var sql = "ALTER TABLE " + SqlBuilder.GetTranslationTableName(tableName) +
                      " ALTER COLUMN " + SqlBuilder.GetTranslationColumnName(columnInfo.DbColumnName) +
                      " TYPE " + SonnetDBSchemaSql.NormalizeDataType(columnInfo.DataType) +
                      " " + nullability;
            Context.Ado.ExecuteCommand(sql);
            return true;
        }

        public override bool AddDefaultValue(string tableName, string columnName, string defaultValue)
        {
            var column = GetColumnInfosByTableName(tableName, false).FirstOrDefault(it =>
                it.DbColumnName.Equals(NormalizeIdentifier(columnName), StringComparison.OrdinalIgnoreCase));
            if (column == null)
            {
                throw new InvalidOperationException(
                    $"SonnetDB 表“{tableName}”中不存在列“{columnName}”。");
            }

            var sql = string.Format(
                AddDefaultValueSql,
                SqlBuilder.GetTranslationTableName(tableName),
                SqlBuilder.GetTranslationColumnName(columnName),
                SonnetDBSchemaSql.FormatDefaultValue(defaultValue, column.DataType));
            Context.Ado.ExecuteCommand(sql);
            return true;
        }

        public bool DropDefaultValue(string tableName, string columnName)
        {
            var column = GetColumnInfosByTableName(tableName, false).FirstOrDefault(it =>
                it.DbColumnName.Equals(NormalizeIdentifier(columnName), StringComparison.OrdinalIgnoreCase));
            if (column == null)
            {
                throw new InvalidOperationException(
                    $"SonnetDB 表“{tableName}”中不存在列“{columnName}”。");
            }

            var sql = "ALTER TABLE " + SqlBuilder.GetTranslationTableName(tableName) +
                      " ALTER COLUMN " + SqlBuilder.GetTranslationColumnName(columnName) +
                      " DROP DEFAULT";
            Context.Ado.ExecuteCommand(sql);
            return true;
        }

        public override bool IsAnyDefaultValue(string tableName, string columnName)
        {
            return GetColumnInfosByTableName(tableName, false).Any(it =>
                it.DbColumnName.Equals(NormalizeIdentifier(columnName), StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(it.DefaultValue));
        }

        public override void AddDefaultValue(EntityInfo entityInfo)
        {
            // 默认值在建表时输出，并由 SonnetDBCodeFirst.ExistLogic 对齐。
        }

        public override bool AddPrimaryKey(string tableName, string columnName)
        {
            throw new NotSupportedException(
                "SonnetDB 代码优先功能不能为已有表新增主键。请创建替代表并迁移数据。");
        }

        public override bool SetAutoIncrementInitialValue(string tableName, int initialValue)
        {
            throw new NotSupportedException(
                "SonnetDB 自增列始终从 1 开始，暂不支持通过 SqlSugar 设置自增初始值。请使用显式整数插入推进自增高水位。");
        }

        public override bool SetAutoIncrementInitialValue(Type entityType, int initialValue)
        {
            throw new NotSupportedException(
                "SonnetDB 自增列始终从 1 开始，暂不支持通过 SqlSugar 设置自增初始值。请使用显式整数插入推进自增高水位。");
        }

        public override bool CreateIndex(string tableName, string[] columnNames, bool isUnique = false)
        {
            if (columnNames == null || columnNames.Length == 0)
            {
                throw new ArgumentException("创建索引至少需要一个列。", nameof(columnNames));
            }

            var indexName = "Index_" + NormalizeIdentifier(tableName) + "_" +
                            string.Join("_", columnNames.Select(NormalizeIdentifier));
            return CreateIndex(tableName, columnNames, indexName, isUnique);
        }

        public override bool CreateUniqueIndex(string tableName, string[] columnNames)
        {
            return CreateIndex(tableName, columnNames, true);
        }

        public override bool CreateIndex(string tableName, string[] columnNames, string indexName, bool isUnique = false)
        {
            if (columnNames == null || columnNames.Length == 0)
            {
                throw new ArgumentException("创建索引至少需要一个列。", nameof(columnNames));
            }

            if (string.IsNullOrWhiteSpace(indexName))
            {
                throw new ArgumentException("索引名称不能为空。", nameof(indexName));
            }

            if (indexName.IndexOf("{include:", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new NotSupportedException("SonnetDB 不支持 SqlSugar 的 INCLUDE 索引列。");
            }

            var normalizedColumns = columnNames.Select(NormalizeIndexColumn).ToArray();
            var sql = string.Format(
                CreateIndexSql,
                SqlBuilder.GetTranslationTableName(tableName),
                string.Join(", ", normalizedColumns),
                SqlBuilder.GetTranslationColumnName(indexName),
                isUnique ? "UNIQUE" : string.Empty);
            Context.Ado.ExecuteCommand(sql);
            return true;
        }

        public override bool DropIndex(string indexName)
        {
            var normalizedIndexName = NormalizeIdentifier(indexName);
            var matchingRow = GetSchema("Indexes").Rows.Cast<DataRow>().FirstOrDefault(row => string.Equals(
                GetString(row, "INDEX_NAME"),
                normalizedIndexName,
                StringComparison.OrdinalIgnoreCase));
            if (matchingRow == null)
            {
                return false;
            }

            return DropIndex(indexName, GetString(matchingRow, "TABLE_NAME"));
        }

        public override bool DropIndex(string indexName, string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException("SonnetDB 删除索引时必须提供表名。", nameof(tableName));
            }

            var sql = "DROP INDEX " + SqlBuilder.GetTranslationColumnName(indexName) +
                      " ON " + SqlBuilder.GetTranslationTableName(tableName);
            Context.Ado.ExecuteCommand(sql);
            return true;
        }

        public override bool DropView(string viewName)
        {
            if (string.IsNullOrWhiteSpace(viewName))
            {
                throw new ArgumentException("视图名称不能为空。", nameof(viewName));
            }

            Context.Ado.ExecuteCommand(
                "DROP VIEW " + SqlBuilder.GetTranslationTableName(viewName));
            return true;
        }

        public override bool DropFunction(string funcName)
        {
            throw new NotSupportedException(
                "SonnetDB 不支持用户定义函数对象，不能通过 SqlSugar 删除函数。");
        }

        public override bool DropProc(string procName)
        {
            if (string.IsNullOrWhiteSpace(procName))
            {
                throw new ArgumentException("过程名称不能为空。", nameof(procName));
            }

            Context.Ado.ExecuteCommand(
                "DROP PROCEDURE " + SqlBuilder.GetTranslationColumnName(procName));
            return true;
        }

        public override bool DropConstraint(string tableName, string constraintName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException("表名称不能为空。", nameof(tableName));
            }

            if (string.IsNullOrWhiteSpace(constraintName))
            {
                throw new ArgumentException("约束名称不能为空。", nameof(constraintName));
            }

            var sql = "ALTER TABLE " + SqlBuilder.GetTranslationTableName(tableName) +
                      " DROP CONSTRAINT " + SqlBuilder.GetTranslationColumnName(constraintName);
            Context.Ado.ExecuteCommand(sql);
            return true;
        }

        public override bool RenameTable(string oldTableName, string newTableName)
        {
            if (string.IsNullOrWhiteSpace(oldTableName))
            {
                throw new ArgumentException("旧表名称不能为空。", nameof(oldTableName));
            }

            if (string.IsNullOrWhiteSpace(newTableName))
            {
                throw new ArgumentException("新表名称不能为空。", nameof(newTableName));
            }

            var sql = "ALTER TABLE " + SqlBuilder.GetTranslationTableName(oldTableName) +
                      " RENAME TO " + SqlBuilder.GetTranslationTableName(newTableName);
            Context.Ado.ExecuteCommand(sql);
            return true;
        }

        public override bool AddRemark(EntityInfo entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity), "实体信息不能为空。");
            }

            var hasTableRemark = !string.IsNullOrWhiteSpace(entity.TableDescription);
            var hasColumnRemark = entity.Columns?.Any(column =>
                !column.IsIgnore && !string.IsNullOrWhiteSpace(column.ColumnDescription)) == true;
            if (hasTableRemark || hasColumnRemark)
            {
                throw new NotSupportedException(
                    "SonnetDB 关系表元数据不支持表备注或列备注，不能通过 SqlSugar 代码优先保存说明。");
            }

            return false;
        }

        public override bool AddColumnRemark(string columnName, string tableName, string description)
        {
            throw new NotSupportedException("SonnetDB 不支持列备注。");
        }

        public override bool DeleteColumnRemark(string columnName, string tableName)
        {
            throw new NotSupportedException("SonnetDB 不支持列备注。");
        }

        public override bool IsAnyColumnRemark(string columnName, string tableName)
        {
            throw new NotSupportedException("SonnetDB 关系表元数据不支持列备注查询。");
        }

        public override bool AddTableRemark(string tableName, string description)
        {
            throw new NotSupportedException("SonnetDB 不支持表备注。");
        }

        public override bool DeleteTableRemark(string tableName)
        {
            throw new NotSupportedException("SonnetDB 不支持表备注。");
        }

        public override bool IsAnyTableRemark(string tableName)
        {
            throw new NotSupportedException("SonnetDB 关系表元数据不支持表备注查询。");
        }

        public override bool BackupDataBase(string databaseName, string fullFileName)
        {
            throw new NotSupportedException("SonnetDB 未通过 SqlSugar DbMaintenance API 提供数据库备份能力。");
        }

        public override bool BackupTable(string oldTableName, string newTableName, int maxBackupDataRows = int.MaxValue)
        {
            throw new NotSupportedException("SonnetDB 提供程序不支持通过查询结果创建表。");
        }

        #endregion

        private string BuildColumnDefinition(DbColumnInfo column)
        {
            var dataType = SonnetDBSchemaSql.NormalizeDataType(column.DataType);
            if (column.IsIdentity && !dataType.Equals("INT", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException("SonnetDB 的自增列必须使用 INT 类型。");
            }

            if (column.IsIdentity && column.DefaultValue != null)
            {
                throw new NotSupportedException("SonnetDB 的自增列不能声明默认值。");
            }

            var parts = new List<string>
            {
                SqlBuilder.GetTranslationColumnName(column.DbColumnName),
                dataType
            };
            if (column.IsIdentity)
            {
                parts.Add(CreateTableIdentity);
            }

            if (column.IsPrimarykey || column.IsIdentity || !column.IsNullable)
            {
                parts.Add(CreateTableNotNull);
            }

            if (column.DefaultValue != null)
            {
                parts.Add("DEFAULT " + SonnetDBSchemaSql.FormatDefaultValue(column.DefaultValue, dataType));
            }

            return string.Join(" ", parts.Where(it => !string.IsNullOrWhiteSpace(it)));
        }

        private SndbConnection GetConnection()
        {
            Context.Ado.CheckConnection();
            var connection = Context.Ado.Connection as SndbConnection;
            if (connection == null)
            {
                throw new InvalidOperationException(
                    "SonnetDBDbMaintenance 需要 SonnetDB.Data.SndbConnection 连接。");
            }

            return connection;
        }

        private DataTable GetSchema(string collectionName, string? tableName = null)
        {
            var connection = GetConnection();
            if (string.IsNullOrWhiteSpace(tableName))
            {
                return connection.GetSchema(collectionName);
            }

            return connection.GetSchema(collectionName, new[] { null, null, tableName, null });
        }

        private List<string> ReadObjectNames(string sql)
        {
            var table = Context.Ado.GetDataTable(sql);
            if (table.Columns.Count == 0)
            {
                return new List<string>();
            }

            return table.Rows.Cast<DataRow>()
                .Select(row => row[0] == DBNull.Value ? null : Convert.ToString(row[0]))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private string? NormalizeIdentifier(string value)
        {
            return SqlBuilder.GetNoTranslationColumnName(value)?.Trim();
        }

        private string NormalizeIndexColumn(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
            {
                throw new ArgumentException("索引列名称不能为空。", nameof(columnName));
            }

            var normalized = columnName.Trim();
            var separatorIndex = normalized.LastIndexOf(' ');
            if (separatorIndex > 0)
            {
                var order = normalized.Substring(separatorIndex + 1);
                if (order.Equals("ASC", StringComparison.OrdinalIgnoreCase) ||
                    order.Equals("DESC", StringComparison.OrdinalIgnoreCase))
                {
                    // SonnetDB 索引元数据不记录排序方向，因此省略该标记。
                    normalized = normalized.Substring(0, separatorIndex).TrimEnd();
                }
            }

            return SqlBuilder.GetTranslationColumnName(NormalizeIdentifier(normalized));
        }

        private static string? GetString(DataRow row, string columnName)
        {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value)
            {
                return null;
            }

            return Convert.ToString(row[columnName]);
        }

        private static bool GetBool(DataRow row, string columnName)
        {
            return row.Table.Columns.Contains(columnName) && row[columnName] != DBNull.Value &&
                   Convert.ToBoolean(row[columnName]);
        }

        private static int GetInt(DataRow row, string columnName)
        {
            return row.Table.Columns.Contains(columnName) && row[columnName] != DBNull.Value
                ? Convert.ToInt32(row[columnName])
                : 0;
        }
    }

    internal static class SonnetDBSchemaSql
    {
        internal static string NormalizeDataType(string dataType)
        {
            var normalized = (dataType ?? string.Empty).Trim();
            var typeEnd = normalized.IndexOf('(');
            if (typeEnd >= 0)
            {
                normalized = normalized.Substring(0, typeEnd).Trim();
            }

            switch (normalized.ToUpperInvariant())
            {
                case "INT":
                case "INTEGER":
                case "BIGINT":
                case "SMALLINT":
                case "TINYINT":
                case "INT16":
                case "INT32":
                case "INT64":
                    return "INT";
                case "FLOAT":
                case "DOUBLE":
                case "REAL":
                    return "FLOAT";
                case "DECIMAL":
                case "NUMERIC":
                    throw new NotSupportedException(
                        "SonnetDB 关系表不支持精确的 DECIMAL/NUMERIC 列。请使用 FLOAT，或改用 STRING 并自行处理精度。");
                case "BOOL":
                case "BOOLEAN":
                case "BIT":
                    return "BOOL";
                case "STRING":
                case "VARCHAR":
                case "NVARCHAR":
                case "CHAR":
                case "NCHAR":
                case "TEXT":
                    return "STRING";
                case "DATETIME":
                case "TIMESTAMP":
                case "DATE":
                    return "DATETIME";
                case "TIME":
                    throw new NotSupportedException(
                        "SonnetDB 关系表不支持 TIME 列。请使用 DATETIME 或 STRING 表示时间值。");
                case "BLOB":
                case "BYTEA":
                case "BINARY":
                case "VARBINARY":
                    return "BLOB";
                case "JSON":
                case "JSONB":
                    return "JSON";
                default:
                    throw new NotSupportedException(
                        $"SonnetDB 不支持 SqlSugar 列类型“{dataType}”。支持的类型为 INT、FLOAT、BOOL、STRING、DATETIME、BLOB 和 JSON。");
            }
        }

        internal static bool AreEquivalentTypes(string left, string right)
        {
            return string.Equals(
                NormalizeDataType(left),
                NormalizeDataType(right),
                StringComparison.OrdinalIgnoreCase);
        }

        internal static string FormatDefaultValue(string defaultValue, string dataType)
        {
            var normalizedType = NormalizeDataType(dataType);
            var value = defaultValue ?? string.Empty;
            var trimmed = value.Trim();
            if (trimmed.Length == 0)
            {
                if (normalizedType == "STRING" || normalizedType == "JSON")
                {
                    return "''";
                }

                throw new NotSupportedException(
                    $"空默认值仅适用于 SonnetDB 的 STRING 或 JSON 列，不能用于 {normalizedType}。");
            }

            if ((trimmed.StartsWith("'", StringComparison.Ordinal) &&
                 trimmed.EndsWith("'", StringComparison.Ordinal)) ||
                trimmed.Equals("NULL", StringComparison.OrdinalIgnoreCase) ||
                IsBooleanLiteral(trimmed) ||
                IsNumericLiteral(trimmed))
            {
                return trimmed;
            }

            if (trimmed.Equals("now()", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("getdate()", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("current_timestamp", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("current_date", StringComparison.OrdinalIgnoreCase))
            {
                return "CURRENT_UTC_DATETIME()";
            }

            if (trimmed.EndsWith(")", StringComparison.Ordinal))
            {
                return trimmed;
            }

            if (normalizedType == "STRING" || normalizedType == "JSON" || normalizedType == "DATETIME")
            {
                return "'" + trimmed.Replace("'", "''") + "'";
            }

            return trimmed;
        }

        internal static bool AreEquivalentDefaults(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) && string.IsNullOrWhiteSpace(right))
            {
                return true;
            }

            return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBooleanLiteral(string value)
        {
            return value.Equals("TRUE", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("FALSE", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsNumericLiteral(string value)
        {
            decimal ignored;
            return decimal.TryParse(
                value,
                System.Globalization.NumberStyles.Number | System.Globalization.NumberStyles.AllowExponent,
                System.Globalization.CultureInfo.InvariantCulture,
                out ignored);
        }
    }
}
