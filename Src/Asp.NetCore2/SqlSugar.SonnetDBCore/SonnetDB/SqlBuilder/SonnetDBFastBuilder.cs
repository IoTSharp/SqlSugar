using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace SqlSugar.SonnetDB
{
    public sealed class SonnetDBFastBuilder : FastBuilder, IFastBuilder
    {
        private bool isStaging;
        private string? stagingTableName;
        private string? targetTableName;

        public override DbFastestProperties DbFastestProperties { get; set; } =
            new DbFastestProperties { IsMerge = true };

        public async Task<int> ExecuteBulkCopyAsync(DataTable table)
        {
            if (table.Rows.Count == 0) return 0;
            var builder = Context.Queryable<object>().SqlBuilder;
            var columns = table.Columns.Cast<DataColumn>()
                .Where(column => isStaging || DbFastestProperties.IsOffIdentity || !FastEntityInfo.Columns.Any(entity =>
                    entity.IsIdentity && string.Equals(entity.DbColumnName, column.ColumnName, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            var names = string.Join(",", columns.Select(column => builder.GetTranslationColumnName(column.ColumnName)));
            var batchSize = DbFastestProperties.BatchSize > 0 ? DbFastestProperties.BatchSize : 1000;
            var result = 0;
            try
            {
                for (var start = 0; start < table.Rows.Count; start += batchSize)
                {
                    var parameters = new List<SugarParameter>();
                    var rows = new List<string>();
                    for (var row = start; row < Math.Min(start + batchSize, table.Rows.Count); row++)
                    {
                        var values = new List<string>();
                        foreach (var column in columns)
                        {
                            var name = "@p" + parameters.Count;
                            parameters.Add(new SugarParameter(name, table.Rows[row][column]));
                            values.Add(name);
                        }
                        rows.Add("(" + string.Join(",", values) + ")");
                    }
                    result += await Context.Ado.ExecuteCommandAsync(
                        "INSERT INTO " + builder.GetTranslationTableName(table.TableName) +
                        " (" + names + ") VALUES " + string.Join(",", rows) +
                        (DbFastestProperties.IsIgnoreInsertError ? " ON CONFLICT DO NOTHING" : ""), parameters.ToArray());
                }
                return result;
            }
            catch
            {
                DropStagingOnFailure();
                throw;
            }
            finally
            {
                CloseDb();
            }
        }

        public override Task CreateTempAsync<T>(DataTable table)
        {
            // 沿用 Fastest 的 staging 生命周期；表结构交给已有维护接口生成。
            targetTableName = table.TableName;
            var columns = Context.DbMaintenance.GetColumnInfosByTableName(table.TableName, false)
                .Where(column => table.Columns.Contains(column.DbColumnName)).ToList();
            foreach (var column in columns)
            {
                column.IsIdentity = false;
                column.IsPrimarykey = false;
                column.ColumnDescription = null;
                column.DefaultValue = null;
            }
            columns.Add(new DbColumnInfo
            {
                DbColumnName = "sonnet_bulk_row_" + Guid.NewGuid().ToString("N"),
                DataType = "INT",
                IsPrimarykey = true,
                IsIdentity = true
            });
            table.TableName = "sonnet_bulk_" + Guid.NewGuid().ToString("N");
            Context.DbMaintenance.CreateTable(table.TableName, columns);
            stagingTableName = table.TableName;
            isStaging = true;
            return Task.CompletedTask;
        }

        public override async Task<int> UpdateByTempAsync(string tableName, string tempName, string[] updateColumns, string[] whereColumns)
        {
            var builder = Context.Queryable<object>().SqlBuilder;
            var sets = string.Join(",", updateColumns.Select(column =>
                builder.GetTranslationColumnName(column) + "=TE." + builder.GetTranslationColumnName(column)));
            try
            {
                return await Context.Ado.ExecuteCommandAsync(
                    "UPDATE " + builder.GetTranslationTableName(targetTableName ?? tableName) + " AS TM SET " + sets +
                    " FROM " + builder.GetTranslationTableName(tempName) + " AS TE WHERE " + JoinKeys(whereColumns));
            }
            catch
            {
                DropStagingOnFailure();
                throw;
            }
        }

        public override async Task<int> Merge<T>(string tableName, DataTable table, EntityInfo entityInfo,
            string[] whereColumns, string[] updateColumns, List<T> datas)
        {
            var builder = Context.Queryable<object>().SqlBuilder;
            var result = await UpdateByTempAsync(tableName, table.TableName, updateColumns, whereColumns);
            var columns = table.Columns.Cast<DataColumn>()
                .Where(column => DbFastestProperties.IsOffIdentity || !entityInfo.Columns.Any(entity => entity.IsIdentity &&
                    string.Equals(entity.DbColumnName, column.ColumnName, StringComparison.OrdinalIgnoreCase))).ToArray();
            var names = string.Join(",", columns.Select(column => builder.GetTranslationColumnName(column.ColumnName)));
            var values = string.Join(",", columns.Select(column => "TE." + builder.GetTranslationColumnName(column.ColumnName)));
            try
            {
                result += await Context.Ado.ExecuteCommandAsync(
                    "INSERT INTO " + builder.GetTranslationTableName(tableName) + " (" + names + ") SELECT " + values +
                    " FROM " + builder.GetTranslationTableName(table.TableName) + " AS TE WHERE NOT EXISTS (SELECT 1 FROM " +
                    builder.GetTranslationTableName(tableName) + " AS TM WHERE " + JoinKeys(whereColumns) + ")");
            }
            catch
            {
                DropStagingOnFailure();
                throw;
            }
            return result;
        }

        private string JoinKeys(string[] columns)
        {
            var builder = Context.Queryable<object>().SqlBuilder;
            return string.Join(" AND ", columns.Select(column =>
                "TM." + builder.GetTranslationColumnName(column) + "=TE." + builder.GetTranslationColumnName(column)));
        }

        private void DropStagingOnFailure()
        {
            if (stagingTableName == null) return;
            try { Context.DbMaintenance.DropTable(stagingTableName); }
            catch { /* 保留原始批量执行错误。 */ }
        }
    }
}
