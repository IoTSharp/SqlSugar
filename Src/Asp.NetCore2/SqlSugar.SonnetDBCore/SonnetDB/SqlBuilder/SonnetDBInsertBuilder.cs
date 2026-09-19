using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using AdoDbType = System.Data.DbType;

namespace SqlSugar.SonnetDB
{
    public sealed class SonnetDBInsertBuilder : InsertBuilder
    {
        public override string SqlTemplate => IsReturnIdentity
            ? "INSERT INTO {0} ({1}) VALUES ({2}) RETURNING $PrimaryKey"
            : "INSERT INTO {0} ({1}) VALUES ({2})";

        public override string SqlTemplateBatch => "INSERT INTO {0} ({1}) VALUES";
        public override string SqlTemplateBatchUnion => ",";
        public override string SqlTemplateBatchSelect => "{0}";

        public override Func<string, string, string> ConvertInsertReturnIdFunc { get; set; } =
            (name, sql) => sql.Trim().TrimEnd(';') + " RETURNING " + name;

        public override string ToSqlString()
        {
            if (IsNoInsertNull)
            {
                DbColumnInfoList = DbColumnInfoList.Where(column => column.Value != null).ToList();
            }

            var groups = DbColumnInfoList.GroupBy(column => column.TableId).ToList();
            if (groups.Count == 0)
            {
                throw new InvalidOperationException("SonnetDB 插入数据时没有可用列。");
            }

            ValidateColumnShape(groups);
            var columns = string.Join(",", groups[0].Select(column => Builder.GetTranslationColumnName(column.DbColumnName)));
            if (groups.Count == 1)
            {
                var values = string.Join(",", groups[0].Select((column, columnIndex) => GetSingleColumn(column, columnIndex)));
                ActionMinDate();
                return string.Format(SqlTemplate, GetTableNameString, columns, values);
            }

            var sql = new StringBuilder();
            sql.AppendFormat(SqlTemplateBatch, GetTableNameString, columns);
            for (var rowIndex = 0; rowIndex < groups.Count; rowIndex++)
            {
                if (rowIndex > 0)
                {
                    sql.Append(SqlTemplateBatchUnion);
                }

                sql.Append(" (");
                var row = groups[rowIndex].ToList();
                for (var columnIndex = 0; columnIndex < row.Count; columnIndex++)
                {
                    if (columnIndex > 0)
                    {
                        sql.Append(",");
                    }

                    sql.Append(GetBatchColumn(row[columnIndex], rowIndex, columnIndex));
                }
                sql.Append(")");
            }

            ActionMinDate();
            return sql.ToString();
        }

        public override string GetDbColumn(DbColumnInfo columnInfo, object name)
        {
            EnsureSupportedColumn(columnInfo);
            return base.GetDbColumn(columnInfo, name);
        }

        private string GetSingleColumn(DbColumnInfo column, int columnIndex)
        {
            EnsureSupportedColumn(column);
            var originalName = Builder.SqlParameterKeyWord + column.DbColumnName;
            var parameterName = Builder.SqlParameterKeyWord + "sonnet_single_" + columnIndex;
            var originalParameter = Parameters.FirstOrDefault(parameter =>
                string.Equals(parameter.ParameterName, originalName, StringComparison.OrdinalIgnoreCase));
            var existingParameter = Parameters.FirstOrDefault(parameter =>
                string.Equals(parameter.ParameterName, parameterName, StringComparison.OrdinalIgnoreCase));
            RemoveColumnParameters(column, parameterName);

            if (!RequiresBaseColumnHandling(column))
            {
                var parameter = originalParameter ?? existingParameter ?? CreateParameter(parameterName, column);
                parameter.ParameterName = parameterName;
                Parameters.Add(parameter);
                return parameterName;
            }

            var parameterCount = Parameters.Count;
            var result = base.GetDbColumn(column, parameterName);
            return NormalizeGeneratedParameter(column, result, parameterName, parameterCount);
        }

        private string GetBatchColumn(DbColumnInfo column, int rowIndex, int columnIndex)
        {
            EnsureSupportedColumn(column);
            var parameterName = Builder.SqlParameterKeyWord + "sonnet_" + rowIndex + "_" + columnIndex;
            RemoveColumnParameters(column, parameterName);
            if (RequiresBaseColumnHandling(column))
            {
                var parameterCount = Parameters.Count;
                var result = base.GetDbColumn(column, null);
                return NormalizeGeneratedParameter(column, result, parameterName, parameterCount);
            }

            Parameters.Add(CreateParameter(parameterName, column));
            return parameterName;
        }

        private string NormalizeGeneratedParameter(
            DbColumnInfo column,
            string result,
            string parameterName,
            int parameterCount)
        {
            var generated = Parameters
                .Skip(parameterCount)
                .FirstOrDefault(parameter =>
                    string.Equals(parameter.ParameterName, result, StringComparison.OrdinalIgnoreCase));
            if (generated != null)
            {
                generated.ParameterName = parameterName;
                return parameterName;
            }

            if (string.Equals(result, parameterName, StringComparison.OrdinalIgnoreCase))
            {
                Parameters.Add(CreateParameter(parameterName, column));
                return parameterName;
            }

            var referenced = Parameters.FirstOrDefault(parameter =>
                string.Equals(parameter.ParameterName, result, StringComparison.OrdinalIgnoreCase));
            if (referenced != null)
            {
                referenced.ParameterName = parameterName;
                return parameterName;
            }

            if (IsParameterReference(result))
            {
                throw new NotSupportedException(
                    $"SonnetDB 插入列“{column.DbColumnName}”生成了未绑定参数“{result}”。");
            }

            return result;
        }

        private void RemoveColumnParameters(DbColumnInfo column, string parameterName)
        {
            var originalName = Builder.SqlParameterKeyWord + column.DbColumnName;
            Parameters.RemoveAll(parameter =>
                string.Equals(parameter.ParameterName, originalName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(parameter.ParameterName, parameterName, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsParameterReference(string value)
        {
            return !string.IsNullOrEmpty(value)
                && (value[0] == '@' || value[0] == ':')
                && value.Length > 1
                && value.Skip(1).All(character => char.IsLetterOrDigit(character) || character == '_');
        }

        private SugarParameter CreateParameter(string parameterName, DbColumnInfo column)
        {
            var propertyType = column.PropertyType ?? column.Value?.GetType() ?? typeof(string);
            var parameter = new SugarParameter(parameterName, column.Value, propertyType)
            {
                IsJson = column.IsJson,
                IsArray = column.IsArray
            };

            if (column.SqlParameterDbType is AdoDbType dbType)
            {
                parameter.DbType = dbType;
            }
            if (column.IsJson)
            {
                Builder.ChangeJsonType(parameter);
            }
            return parameter;
        }

        private static bool RequiresBaseColumnHandling(DbColumnInfo column)
        {
            return column.InsertServerTime
                || !string.IsNullOrWhiteSpace(column.InsertSql)
                || column.SqlParameterDbType is Type
                || string.Equals(column.DataType, "nvarchar2", StringComparison.OrdinalIgnoreCase)
                || string.Equals(column.PropertyType?.Name, "DateOnly", StringComparison.Ordinal);
        }

        private static void EnsureSupportedColumn(DbColumnInfo column)
        {
            if (string.Equals(column.PropertyType?.Name, "TimeOnly", StringComparison.Ordinal))
            {
                throw new NotSupportedException("SonnetDB 当前不支持 TimeOnly 列。");
            }
            if (column.IsArray)
            {
                throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 数组列。");
            }
        }

        private static void ValidateColumnShape(IReadOnlyList<IGrouping<int, DbColumnInfo>> groups)
        {
            var firstColumns = groups[0].Select(column => column.DbColumnName).ToList();
            foreach (var group in groups.Skip(1))
            {
                var columns = group.Select(column => column.DbColumnName).ToList();
                if (columns.Count != firstColumns.Count || !columns.SequenceEqual(firstColumns, StringComparer.OrdinalIgnoreCase))
                {
                    throw new NotSupportedException("SonnetDB 的批量插入要求每一行具有相同的列集合。");
                }
            }
        }
    }
}
