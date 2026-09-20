using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using SonnetDB.Data;

namespace SqlSugar.SonnetDB
{
    public sealed class SonnetDBFastBuilder : FastBuilder, IFastBuilder
    {
        public override DbFastestProperties DbFastestProperties { get; set; } =
            new DbFastestProperties { IsConvertDateTimeOffsetToUtcDateTime = true };

        public override async Task<int> Merge<T>(
            string tableName,
            DataTable table,
            EntityInfo entityInfo,
            string[] whereColumns,
            string[] updateColumns,
            List<T> datas)
        {
            if (datas == null || datas.Count == 0)
            {
                return 0;
            }

            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException("合并写入的表名不能为空。", nameof(tableName));
            }
            if (whereColumns == null || whereColumns.Length == 0)
            {
                throw new ArgumentException("合并写入至少需要一个匹配列。", nameof(whereColumns));
            }
            if (updateColumns == null || updateColumns.Length == 0)
            {
                throw new ArgumentException("合并写入至少需要一个更新列。", nameof(updateColumns));
            }

            // SonnetDB 不支持临时表；使用普通 Storageable 逐页完成匹配、插入和更新。
            var result = 0;
            await Context.Utilities.PageEachAsync(datas, 2000, async pageItems =>
            {
                var storage = await Context.Storageable(pageItems)
                    .As(tableName)
                    .WhereColumns(whereColumns)
                    .ToStorageAsync();

                if (storage.InsertList?.Count > 0)
                {
                    result += await storage.AsInsertable.ExecuteCommandAsync();
                }

                if (storage.UpdateList?.Count > 0)
                {
                    foreach (var update in storage.UpdateList)
                    {
                        result += await Context.Updateable(update.Item)
                            .AS(tableName)
                            .WhereColumns(whereColumns)
                            .UpdateColumns(updateColumns)
                            .ExecuteCommandAsync();
                    }
                }
            });
            return result;
        }

        public async Task<int> ExecuteBulkCopyAsync(DataTable table)
        {
            if (table == null || table.Rows.Count == 0)
            {
                return 0;
            }

            var identityColumns = new HashSet<string>(FastEntityInfo.Columns
                .Where(column => column.IsIdentity)
                .Select(column => column.DbColumnName)
                .Where(column => !string.IsNullOrWhiteSpace(column)), StringComparer.OrdinalIgnoreCase);

            try
            {
                var connection = (SndbConnection)Context.Ado.Connection;
                if (connection.State != ConnectionState.Open)
                {
                    await connection.OpenAsync();
                }

                var columns = table.Columns.Cast<DataColumn>()
                    .Where(column => DbFastestProperties?.IsOffIdentity == true || !identityColumns.Contains(column.ColumnName))
                    .ToList();
                if (columns.Count == 0)
                {
                    throw new ArgumentException("批量写入至少需要一个非自增列。", nameof(table));
                }

                ThrowIfUnsupportedColumns(columns);
                ThrowIfUnsupportedValues(table.Rows, columns);

                var names = string.Join(", ", columns.Select(column => Quote(column.ColumnName)));
                var values = string.Join(", ", columns.Select((_, index) => "@p" + index));
                using var command = connection.CreateCommand();
                command.CommandText = "INSERT INTO " + QuoteTableName(table.TableName) + " (" + names + ") VALUES (" + values + ")";

                if (Context.Ado.Transaction != null)
                {
                    command.Transaction = Context.Ado.Transaction as SndbTransaction
                        ?? throw new InvalidOperationException("SonnetDB 批量写入只能使用 SndbTransaction。");
                }

                foreach (DataRow row in table.Rows)
                {
                    command.Parameters.Clear();
                    for (var index = 0; index < columns.Count; index++)
                    {
                        var value = row.IsNull(columns[index]) ? DBNull.Value : row[columns[index]];
                        ThrowIfUnsupportedValue(value);
                        command.Parameters.Add(new SndbParameter("@p" + index, NormalizeValue(value)));
                    }

                    await command.ExecuteNonQueryAsync();
                }

                return table.Rows.Count;
            }
            finally
            {
                CloseDb();
            }
        }

        public override Task<int> UpdateByTempAsync(string tableName, string tempName, string[] updateColumns, string[] whereColumns)
        {
            throw new NotSupportedException("SonnetDB 不支持基于临时表的 SqlSugar 批量更新。");
        }

        public override Task CreateTempAsync<T>(DataTable table)
        {
            throw new NotSupportedException("SonnetDB 不支持临时表。");
        }

        private static string Quote(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new ArgumentException("批量写入的标识符不能为空。", nameof(identifier));
            }

            if (IsQuoted(identifier))
            {
                return identifier;
            }

            return "\"" + identifier.Replace("\"", "\"\"") + "\"";
        }

        private static string QuoteTableName(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException("批量写入的表名不能为空。", nameof(tableName));
            }

            SonnetDBIdentifier.ThrowIfCrossDatabaseReference(tableName);

            return IsQuoted(tableName)
                ? tableName
                : string.Join(".", tableName.Split('.').Select(Quote));
        }

        private static bool IsQuoted(string identifier)
        {
            return identifier.Length >= 2 && identifier[0] == '"' && identifier[identifier.Length - 1] == '"';
        }

        private void ThrowIfUnsupportedColumns(IReadOnlyCollection<DataColumn> columns)
        {
            foreach (var dataColumn in columns)
            {
                var entityColumn = FastEntityInfo.Columns.FirstOrDefault(column =>
                    !column.IsIgnore &&
                    string.Equals(
                        column.DbColumnName ?? column.PropertyName,
                        dataColumn.ColumnName,
                        StringComparison.OrdinalIgnoreCase));
                if (entityColumn != null)
                {
                    ThrowIfUnsupportedType(entityColumn.UnderType);
                }
            }
        }

        private static void ThrowIfUnsupportedValues(DataRowCollection rows, IReadOnlyCollection<DataColumn> columns)
        {
            foreach (var column in columns)
            {
                ThrowIfUnsupportedType(column.DataType);
            }

            foreach (DataRow row in rows)
            {
                foreach (var column in columns)
                {
                    if (!row.IsNull(column))
                    {
                        ThrowIfUnsupportedValue(row[column]);
                    }
                }
            }
        }

        private static void ThrowIfUnsupportedType(Type type)
        {
            var normalizedType = Nullable.GetUnderlyingType(type) ?? type;
            if (normalizedType == typeof(decimal))
            {
                throw new NotSupportedException(
                    "SonnetDB 关系表不支持精确的 DECIMAL/NUMERIC 列。请使用 double 或 float，或使用 STRING 并自行处理精度。");
            }

            if (normalizedType == typeof(TimeOnly) || normalizedType == typeof(TimeSpan))
            {
                throw new NotSupportedException(
                    "SonnetDB 关系表不支持 TimeOnly 或 TimeSpan 列。请使用 DateTime 或 STRING 表示时间值。");
            }
        }

        private static object NormalizeValue(object value)
        {
            return value switch
            {
                DateTimeOffset dateTimeOffset => dateTimeOffset.UtcDateTime,
                DateOnly dateOnly => dateOnly.ToDateTime(TimeOnly.MinValue),
                _ => value
            };
        }

        private static void ThrowIfUnsupportedValue(object value)
        {
            ThrowIfUnsupportedType(value.GetType());
        }
    }
}
