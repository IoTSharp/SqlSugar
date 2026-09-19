using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SqlSugar.SonnetDB
{
    public class SonnetDBBuilder : SqlBuilderProvider
    {
        public override string SqlTranslationLeft
        {
            get
            {
                return "\"";
            }
        }
        public override string SqlTranslationRight
        {
            get
            {
                return "\"";
            }
        }
        public override string SqlDateNow
        {
            get
            {
                return "CURRENT_UTC_DATETIME()";
            }
        }
        public override string FullSqlDateNow
        {
            get
            {
                return "SELECT CURRENT_UTC_DATETIME()";
            }
        }

        public override string GetTranslationColumnName(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                throw new ArgumentException("列名不能为空。", nameof(propertyName));
            }

            if (propertyName.Contains(".", StringComparison.Ordinal) && !propertyName.Contains(SqlTranslationLeft, StringComparison.Ordinal))
            {
                return string.Join(".", propertyName.Split('.').Select(GetTranslationText));
            }

            return propertyName.Contains(SqlTranslationLeft, StringComparison.Ordinal)
                ? propertyName
                : GetTranslationText(propertyName);
        }

        public override string GetTranslationColumnName(string entityName, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(entityName))
            {
                throw new ArgumentException("实体名不能为空。", nameof(entityName));
            }
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                throw new ArgumentException("列名不能为空。", nameof(propertyName));
            }

            var context = this.Context;
            var mappingInfo = context
                 .MappingColumns
                 .FirstOrDefault(it =>
                 it.EntityName.Equals(entityName, StringComparison.CurrentCultureIgnoreCase) &&
                 it.PropertyName.Equals(propertyName, StringComparison.CurrentCultureIgnoreCase));
            return GetTranslationText(mappingInfo == null ? propertyName : mappingInfo.DbColumnName);
        }

        public override string GetTranslationTableName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("表名不能为空。", nameof(name));
            }
            SonnetDBIdentifier.ThrowIfCrossDatabaseReference(name);

            var context = this.Context;

            var mappingInfo = context
                .MappingTables
                .FirstOrDefault(it => it.EntityName.Equals(name, StringComparison.CurrentCultureIgnoreCase));
            if (mappingInfo == null && name.Contains(".", StringComparison.Ordinal) && name.Contains("\"", StringComparison.Ordinal))
            {
                return name;
            }
            name = (mappingInfo == null ? name : mappingInfo.DbTableName);
            SonnetDBIdentifier.ThrowIfCrossDatabaseReference(name);
            if (name.Contains(".", StringComparison.Ordinal) && !name.Contains("(", StringComparison.Ordinal) && !name.Contains("\".\"", StringComparison.Ordinal))
            {
                return string.Join(".", name.Split('.').Select(GetTranslationText));
            }
            else if (name.Contains("(", StringComparison.Ordinal))
            {
                return name;
            }
            else if (name.Contains(SqlTranslationLeft, StringComparison.Ordinal) && name.Contains(SqlTranslationRight, StringComparison.Ordinal))
            {
                return name;
            }
            else
            {
                return GetTranslationText(name.TrimEnd('"').TrimStart('"'));
            }
        }
        public override string GetUnionFomatSql(string sql)
        {
            // SonnetDB 的派生表要求 FROM 后直接跟 SELECT，不能在每个 UNION 分支外再包一层括号。
            return RemoveN(sql);
        }

        public override string RemoveN(string sql)
        {
            if (string.IsNullOrEmpty(sql))
            {
                return sql;
            }

            var result = new StringBuilder(sql.Length);
            var inSingleQuote = false;
            var inDoubleQuote = false;
            var inLineComment = false;
            var inBlockComment = false;

            for (var index = 0; index < sql.Length; index++)
            {
                var character = sql[index];
                var next = index + 1 < sql.Length ? sql[index + 1] : '\0';

                if (inLineComment)
                {
                    result.Append(character);
                    if (character == '\r' || character == '\n')
                    {
                        inLineComment = false;
                    }
                    continue;
                }

                if (inBlockComment)
                {
                    result.Append(character);
                    if (character == '*' && next == '/')
                    {
                        result.Append(next);
                        index++;
                        inBlockComment = false;
                    }
                    continue;
                }

                if (!inSingleQuote && !inDoubleQuote && character == '-' && next == '-')
                {
                    result.Append(character).Append(next);
                    index++;
                    inLineComment = true;
                    continue;
                }

                if (!inSingleQuote && !inDoubleQuote && character == '/' && next == '*')
                {
                    result.Append(character).Append(next);
                    index++;
                    inBlockComment = true;
                    continue;
                }

                if (character == '\'' && !inDoubleQuote)
                {
                    result.Append(character);
                    if (inSingleQuote && next == '\'')
                    {
                        result.Append(next);
                        index++;
                    }
                    else
                    {
                        inSingleQuote = !inSingleQuote;
                    }
                    continue;
                }

                if (character == '"' && !inSingleQuote)
                {
                    result.Append(character);
                    if (inDoubleQuote && next == '"')
                    {
                        result.Append(next);
                        index++;
                    }
                    else
                    {
                        inDoubleQuote = !inDoubleQuote;
                    }
                    continue;
                }

                if (!inSingleQuote && !inDoubleQuote &&
                    (character == 'N' || character == 'n') && next == '\'' &&
                    (index == 0 || !IsIdentifierCharacter(sql[index - 1])))
                {
                    result.Append('\'');
                    index++;
                    inSingleQuote = true;
                    continue;
                }

                result.Append(character);
            }

            return result.ToString();
        }

        private static bool IsIdentifierCharacter(char character)
        {
            return char.IsLetterOrDigit(character) || character == '_';
        }

        public override string GetUnionAllSql(List<string> sqlList)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 UNION ALL 集合运算。");
        }

        public override KeyValuePair<string, SugarParameter[]> ConditionalModelToSql(
            List<IConditionalModel> models,
            int beginIndex = 0)
        {
            ThrowIfILikeEnabled();
            var result = base.ConditionalModelToSql(models, beginIndex);
            var replacements = SonnetDBParameterNames.RenameUnsafe(
                result.Value,
                SqlParameterKeyWord,
                "condition");
            var sql = SonnetDBParameterNames.ReplaceOutsideQuotes(result.Key, replacements);
            return new KeyValuePair<string, SugarParameter[]>(sql, result.Value);
        }

        public override Type GetNullType(string tableName, string columnName)
        {
            if (tableName != null)
                tableName = tableName.Trim();
            columnName = columnName?.Trim().Trim('"');
            var columnInfo = this.Context.DbMaintenance.GetColumnInfosByTableName(tableName)
                .FirstOrDefault(z => string.Equals(z.DbColumnName, columnName, StringComparison.OrdinalIgnoreCase));
            if (columnInfo != null)
            {
                var cTypeName=this.Context.Ado.DbBind.GetCsharpTypeNameByDbTypeName(columnInfo.DataType);
                var value=SqlSugar.UtilMethods.GetTypeByTypeName(cTypeName);
                if (value != null)
                {
                    var key = "GetNullType_" + tableName + columnName;
                    return new ReflectionInoCacheService().GetOrCreate(key, () => value);
                }
            }
            return null!;
        }

        private string GetTranslationText(string name)
        {
            return SqlTranslationLeft + name.Replace("\"", "\"\"", StringComparison.Ordinal) + SqlTranslationRight;
        }

        private void ThrowIfILikeEnabled()
        {
            if (Context.CurrentConnectionConfig.MoreSettings?.EnableILike == true)
            {
                throw new NotSupportedException("SonnetDB 当前不支持 MoreSettings.EnableILike。不区分大小写匹配请在应用层处理。");
            }
        }

    }
}
