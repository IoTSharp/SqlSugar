using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SqlSugar.SonnetDB
{
    public partial class SonnetDBQueryBuilder : QueryBuilder
    {
        private const string OffsetTemplate = "SELECT {0} FROM {1} {2} {3} {4} OFFSET {5}";

        public override string PageTempalte
        {
            get
            {
                return "SELECT {0} FROM {1} {2} {3} {4} LIMIT {6} OFFSET {5}";
            }
        }

        public override string DefaultOrderByTemplate
        {
            get
            {
                return "ORDER BY ";
            }
        }

        public override bool IsComplexModel(string sql)
        {
            return Regex.IsMatch(sql, "AS \\\"\\w+\\.\\w+\\\"") || Regex.IsMatch(sql, "AS \\\"\\w+\\.\\w+\\.\\w+\\\"");
        }

        public override string ToJoinString(JoinQueryInfo joinInfo)
        {
            if (joinInfo == null)
            {
                throw new ArgumentNullException(nameof(joinInfo), "连接信息不能为空。");
            }

            if (joinInfo.JoinType != JoinType.Inner && joinInfo.JoinType != JoinType.Left)
            {
                throw new NotSupportedException("SonnetDB 当前仅支持 INNER JOIN 和 LEFT JOIN，不支持该连接类型。");
            }

            return base.ToJoinString(joinInfo);
        }

        public override string ToSqlString()
        {
            if (Context.CurrentConnectionConfig.MoreSettings?.EnableILike == true)
            {
                throw new NotSupportedException("SonnetDB 当前不支持 MoreSettings.EnableILike。不区分大小写匹配请在应用层处理。");
            }
            if (Context.CurrentConnectionConfig.MoreSettings?.IsWithNoLockQuery == true)
            {
                throw new NotSupportedException("SonnetDB 当前不支持 MoreSettings.IsWithNoLockQuery 全局无锁查询配置。");
            }
            if (!string.IsNullOrWhiteSpace(Hints))
            {
                throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 Hints 表提示。请移除数据库专用提示后重试。");
            }

            if (!string.IsNullOrWhiteSpace(PartitionByValue))
            {
                throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
            }
            if (!string.IsNullOrWhiteSpace(TranLock))
            {
                throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的事务行锁语义。");
            }
            if (Skip < 0)
            {
                throw new NotSupportedException("SonnetDB 分页的 Skip 参数不能小于 0。");
            }
            if (Take < 0)
            {
                throw new NotSupportedException("SonnetDB 分页的 Take 参数不能小于 0。");
            }

            base.AppendFilter();
            var oldOrderValue = OrderByValue;
            try
            {
                var hasPaging = Skip != null || Take != null;
                if (hasPaging && IsEmptyOrderBy(OrderByValue))
                {
                    OrderByValue = OrderByTemplate + GetPagingOrderByColumn();
                }

                var selectValue = GetSelectValue;
                var tableName = GetTableNameString;
                var whereValue = GetWhereValueString;
                var groupByValue = GetGroupByString + HavingInfos;
                var orderByValue = hasPaging ? null : GetOrderByString;

                sql = new StringBuilder();
                sql.AppendFormat(SqlTemplate, selectValue, tableName, whereValue, groupByValue, orderByValue);
                if (IsCount)
                {
                    return sql.ToString();
                }

                string result;
                if (Skip != null && Take == null)
                {
                    result = string.Format(
                        OffsetTemplate,
                        selectValue,
                        tableName,
                        whereValue,
                        groupByValue,
                        GetOrderByString,
                        Math.Max(0, Skip.Value));
                }
                else if (Skip == null && Take != null)
                {
                    result = string.Format(
                        PageTempalte,
                        selectValue,
                        tableName,
                        whereValue,
                        groupByValue,
                        GetOrderByString,
                        0,
                        Take.Value);
                }
                else if (Skip != null && Take != null)
                {
                    result = string.Format(
                        PageTempalte,
                        selectValue,
                        tableName,
                        whereValue,
                        groupByValue,
                        GetOrderByString,
                        Math.Max(0, Skip.Value),
                        Take.Value);
                }
                else
                {
                    result = sql.ToString();
                }

                result = GetSqlQuerySql(result);
                return result.IndexOf("-- No table", StringComparison.Ordinal) >= 0 ? "-- No table" : result;
            }
            finally
            {
                OrderByValue = oldOrderValue;
            }
        }

        public override string GetSelectValue
        {
            get
            {
                var result = SelectValue == null || SelectValue is string
                    ? GetSelectValueByString()
                    : GetSelectValueByExpression();

                if (SelectType == ResolveExpressType.SelectMultiple)
                {
                    SelectCacheKey += string.Join("-", JoinQueryInfos.Select(it => it.TableName));
                }
                if (IsDistinct && !result.TrimStart().StartsWith("distinct ", StringComparison.OrdinalIgnoreCase))
                {
                    result = "distinct " + result;
                }
                if (SubToListParameters != null && SubToListParameters.Any())
                {
                    result = SubToListMethod(result);
                }
                return result;
            }
        }

        private static bool IsEmptyOrderBy(string orderByValue)
        {
            return string.IsNullOrWhiteSpace(orderByValue)
                || string.Equals(orderByValue.Trim(), "ORDER BY", StringComparison.OrdinalIgnoreCase);
        }

        private string GetPagingOrderByColumn()
        {
            var selectValue = GetSelectValue?.Trim();
            if (string.IsNullOrWhiteSpace(selectValue))
            {
                throw new NotSupportedException("SonnetDB 分页需要可排序的投影列，请显式调用 OrderBy。");
            }

            if (selectValue.StartsWith("distinct ", StringComparison.OrdinalIgnoreCase))
            {
                selectValue = selectValue.Substring("distinct ".Length).TrimStart();
            }

            var firstSelectItem = GetFirstSelectItem(selectValue);
            if (IsWildcard(firstSelectItem))
            {
                return GetFirstEntityColumn();
            }

            var alias = Regex.Match(firstSelectItem, @"\s+AS\s+(?<name>.+)$", RegexOptions.IgnoreCase);
            if (alias.Success && IsSimpleColumn(alias.Groups["name"].Value.Trim()))
            {
                return alias.Groups["name"].Value.Trim();
            }

            if (IsSimpleColumn(firstSelectItem))
            {
                // Keep a qualified projection such as "d"."Id" qualified.  Removing
                // the table prefix makes the automatically generated paging order
                // ambiguous as soon as another joined table exposes an Id column.
                return firstSelectItem;
            }

            if (IsConstant(firstSelectItem))
            {
                return GetFirstEntityColumn();
            }

            throw new NotSupportedException("SonnetDB 分页的首个投影不是可排序列，请显式调用 OrderBy。");
        }

        private string GetFirstEntityColumn()
        {
            if (EntityType == null || EntityType == typeof(object))
            {
                throw new NotSupportedException("SonnetDB 对原始 SQL 分页需要显式调用 OrderBy。");
            }

            var column = Context.EntityMaintenance.GetEntityInfo(EntityType).Columns
                .FirstOrDefault(it => !it.IsIgnore);
            if (column == null || string.IsNullOrWhiteSpace(column.DbColumnName))
            {
                throw new NotSupportedException("SonnetDB 无法确定分页排序列，请显式调用 OrderBy。");
            }

            var translatedColumn = Builder.GetTranslationColumnName(column.DbColumnName);
            if (JoinQueryInfos != null && JoinQueryInfos.Any() && !string.IsNullOrWhiteSpace(TableShortName))
            {
                return Builder.GetTranslationColumnName(TableShortName) + "." + translatedColumn;
            }

            return translatedColumn;
        }

        private static bool IsWildcard(string selectItem)
        {
            var value = selectItem.Trim();
            return value == "*" || value.EndsWith(".*", StringComparison.Ordinal);
        }

        private static bool IsSimpleColumn(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return Regex.IsMatch(
                value.Trim(),
                "^(?:\\\"[^\\\"]+\\\"|[A-Za-z_][A-Za-z0-9_]*)(?:\\.(?:\\\"[^\\\"]+\\\"|[A-Za-z_][A-Za-z0-9_]*))*$");
        }

        private static bool IsConstant(string value)
        {
            var trimmed = value.Trim();
            return Regex.IsMatch(trimmed, "^(?:[-+]?\\d+(?:\\.\\d+)?|NULL|TRUE|FALSE|'(?:''|[^'])*'|\\\"(?:\\\"\\\"|[^\\\"])*\\\")$", RegexOptions.IgnoreCase);
        }

        private static string GetFirstSelectItem(string selectValue)
        {
            var parentheses = 0;
            var isInSingleQuote = false;
            var isInDoubleQuote = false;
            for (var index = 0; index < selectValue.Length; index++)
            {
                var current = selectValue[index];
                if (current == '\'' && !isInDoubleQuote)
                {
                    isInSingleQuote = !isInSingleQuote;
                }
                else if (current == '"' && !isInSingleQuote)
                {
                    isInDoubleQuote = !isInDoubleQuote;
                }
                else if (!isInSingleQuote && !isInDoubleQuote)
                {
                    if (current == '(')
                    {
                        parentheses++;
                    }
                    else if (current == ')')
                    {
                        parentheses--;
                    }
                    else if (current == ',' && parentheses == 0)
                    {
                        return selectValue.Substring(0, index).Trim();
                    }
                }
            }

            return selectValue.Trim();
        }
    }
}
