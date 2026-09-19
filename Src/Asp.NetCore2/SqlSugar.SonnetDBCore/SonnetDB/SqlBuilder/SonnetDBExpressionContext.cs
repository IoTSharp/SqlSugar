using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using SonnetDB.Documents;

namespace SqlSugar.SonnetDB
{
    public class SonnetDBExpressionContext : ExpressionContext, ILambdaExpressions
    {
        public SqlSugarProvider Context { get; set; }

        public SonnetDBExpressionContext()
        {
            DbMehtods = new SonnetDBMethod();
        }

        void ILambdaExpressions.Resolve(Expression expression, ResolveExpressType resolveType)
        {
            ThrowIfUnsupportedBitwiseOperator(expression);
            base.Resolve(expression, resolveType);
        }

        public override string GetLimit()
        {
            return " LIMIT 1 OFFSET 0 ";
        }

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

        public override string GetTranslationText(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("标识符不能为空。", nameof(name));
            }

            return SqlTranslationLeft + EscapeIdentifier(name) + SqlTranslationRight;
        }

        public override string GetTranslationTableName(string entityName, bool isMapping = true)
        {
            if (string.IsNullOrWhiteSpace(entityName))
            {
                throw new ArgumentException("实体表名不能为空。", nameof(entityName));
            }
            SonnetDBIdentifier.ThrowIfCrossDatabaseReference(entityName);
            if (IsTranslationText(entityName))
            {
                return entityName;
            }

            var tableName = entityName;
            if (isMapping && MappingTables != null && MappingTables.Count > 0)
            {
                var mapping = MappingTables.FirstOrDefault(it => string.Equals(
                    it.EntityName,
                    entityName,
                    StringComparison.OrdinalIgnoreCase));
                if (mapping != null && !string.IsNullOrWhiteSpace(mapping.DbTableName))
                {
                    tableName = mapping.DbTableName;
                }
            }

            SonnetDBIdentifier.ThrowIfCrossDatabaseReference(tableName);

            if (tableName.Contains("(", StringComparison.Ordinal))
            {
                return tableName;
            }

            return TranslateQualifiedName(tableName);
        }

        public override string GetTranslationColumnName(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
            {
                throw new ArgumentException("列名不能为空。", nameof(columnName));
            }
            if (columnName.StartsWith(SqlParameterKeyWord, StringComparison.Ordinal) || columnName.StartsWith("@", StringComparison.Ordinal))
            {
                return columnName;
            }
            if (IsTranslationText(columnName))
            {
                return columnName;
            }

            return TranslateQualifiedName(columnName);
        }

        public override string GetDbColumnName(string entityName, string propertyName)
        {
            var columnName = propertyName;
            if (MappingColumns != null && MappingColumns.Count > 0)
            {
                var mapping = MappingColumns.FirstOrDefault(it =>
                    string.Equals(it.EntityName, entityName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(it.PropertyName, propertyName, StringComparison.OrdinalIgnoreCase));
                if (mapping != null && !string.IsNullOrWhiteSpace(mapping.DbColumnName))
                {
                    columnName = mapping.DbColumnName;
                }
            }

            return NormalizeIdentifier(columnName);
        }

        private string TranslateQualifiedName(string name)
        {
            if (name.Contains('.', StringComparison.Ordinal))
            {
                return string.Join(".", name.Split('.').Select(GetTranslationText));
            }

            return GetTranslationText(name);
        }

        private string NormalizeIdentifier(string name)
        {
            return name;
        }

        private static string EscapeIdentifier(string name)
        {
            return name.Replace("\"", "\"\"", StringComparison.Ordinal);
        }

        private static void ThrowIfUnsupportedBitwiseOperator(Expression expression)
        {
            new BitwiseOperatorVisitor().Visit(expression);
        }

        private sealed class BitwiseOperatorVisitor : ExpressionVisitor
        {
            protected override Expression VisitBinary(BinaryExpression node)
            {
                if (node.NodeType is ExpressionType.And or ExpressionType.Or)
                {
                    throw new NotSupportedException(
                        "SonnetDB 当前不支持按位 & 或 | 运算，也不支持使用 & 或 | 表示布尔条件。请改用 && 或 ||，或在应用层计算。");
                }

                return base.VisitBinary(node);
            }
        }
    }

    public class SonnetDBMethod : DefaultDbMethod, IDbMethods
    {
        public override string IIF(MethodCallExpressionModel model)
        {
            return base.IIF(model);
        }

        public override string Contains(MethodCallExpressionModel model)
        {
            return $"({model.Args[0].MemberName} LIKE CONCAT('%', {model.Args[1].MemberName}, '%'))";
        }

        public override string StartsWith(MethodCallExpressionModel model)
        {
            return $"({model.Args[0].MemberName} LIKE CONCAT({model.Args[1].MemberName}, '%'))";
        }

        public override string EndsWith(MethodCallExpressionModel model)
        {
            return $"({model.Args[0].MemberName} LIKE CONCAT('%', {model.Args[1].MemberName}))";
        }

        public override string BitwiseAnd(MethodCallExpressionModel model)
        {
            return Unsupported("按位与运算");
        }

        public override string BitwiseInclusiveOR(MethodCallExpressionModel model)
        {
            return Unsupported("按位或运算");
        }

        public override string ContainsArray(MethodCallExpressionModel model)
        {
            if (TryGetBooleanValues(model, out var values))
            {
                var field = model.Args[1].MemberName;
                var literals = string.Join(",", values.Select(value => value ? "TRUE" : "FALSE"));
                return $" ({field} IN ({literals})) ";
            }

            return base.ContainsArray(model);
        }

        public override string True()
        {
            return "TRUE";
        }

        public override string False()
        {
            return "FALSE";
        }

        public override string TrueValue()
        {
            return "TRUE";
        }

        public override string FalseValue()
        {
            return "FALSE";
        }

        public override string DateValue(MethodCallExpressionModel model)
        {
            return $"DATE_PART('{GetDatePart(GetDateType(model.Args[1]))}', {model.Args[0].MemberName})";
        }

        public override string DateIsSameDay(MethodCallExpressionModel model)
        {
            return $"(DATE_ONLY({model.Args[0].MemberName}) = DATE_ONLY({model.Args[1].MemberName}))";
        }

        public override string DateIsSameByType(MethodCallExpressionModel model)
        {
            var left = model.Args[0].MemberName;
            var right = model.Args[1].MemberName;
            return GetDateType(model.Args[2]) switch
            {
                DateType.Year => EqualDatePart("year", left, right),
                DateType.Quarter => EqualDatePart("year", left, right) + " AND " + EqualDatePart("quarter", left, right),
                DateType.Month => EqualDatePart("year", left, right) + " AND " + EqualDatePart("month", left, right),
                DateType.Day => $"DATE_ONLY({left}) = DATE_ONLY({right})",
                DateType.Hour => EqualDateOnlyAndParts(left, right, "hour"),
                DateType.Minute => EqualDateOnlyAndParts(left, right, "hour", "minute"),
                DateType.Second => EqualDateOnlyAndParts(left, right, "hour", "minute", "second"),
                DateType.Millisecond => EqualDateOnlyAndParts(left, right, "hour", "minute", "second", "millisecond"),
                DateType.Weekday => EqualDatePart("day_of_week", left, right),
                _ => Unsupported("日期比较")
            };
        }

        public override string DateAddDay(MethodCallExpressionModel model)
        {
            return $"DATE_ADD({model.Args[0].MemberName}, {model.Args[1].MemberName}, 'day')";
        }

        public override string DateAddByType(MethodCallExpressionModel model)
        {
            var date = model.Args[0].MemberName;
            var amount = model.Args[1].MemberName;
            return GetDateType(model.Args[2]) switch
            {
                DateType.Year => $"DATE_ADD({date}, {amount}, 'year')",
                DateType.Quarter => $"DATE_ADD({date}, ({amount} * 3), 'month')",
                DateType.Month => $"DATE_ADD({date}, {amount}, 'month')",
                DateType.Day => $"DATE_ADD({date}, {amount}, 'day')",
                DateType.Hour => $"DATE_ADD({date}, {amount}, 'hour')",
                DateType.Minute => $"DATE_ADD({date}, {amount}, 'minute')",
                DateType.Second => $"DATE_ADD({date}, {amount}, 'second')",
                DateType.Millisecond => $"DATE_ADD({date}, {amount}, 'millisecond')",
                DateType.Weekday => Unsupported("按星期日期加法"),
                _ => Unsupported("日期加法")
            };
        }

        public override string ToDate(MethodCallExpressionModel model)
        {
            if (model.Args[0].MemberValue is string || model.Args[0].Type == typeof(string))
            {
                return Unsupported("字符串转日期");
            }

            return $"TO_UTC_DATETIME({model.Args[0].MemberName})";
        }

        public override string ToDateShort(MethodCallExpressionModel model)
        {
            return $"DATE_ONLY({model.Args[0].MemberName})";
        }

        public override string ToString(MethodCallExpressionModel model)
        {
            return $"CONCAT('', {model.Args[0].MemberName})";
        }

        public override string ToVarchar(MethodCallExpressionModel model)
        {
            return ToString(model);
        }

        public override string MergeString(params string[] strings)
        {
            return "CONCAT(" + string.Join(",", strings) + ")";
        }

        public override string IsNull(MethodCallExpressionModel model)
        {
            return $"COALESCE({model.Args[0].MemberName}, {model.Args[1].MemberName})";
        }

        public override string GetDate()
        {
            return "CURRENT_UTC_DATETIME()";
        }

        public override string EqualTrue(string fieldName)
        {
            return "(" + fieldName + " = TRUE)";
        }

        public override string UNIX_TIMESTAMP(MethodCallExpressionModel model)
        {
            return $"TO_UNIX_SECONDS({model.Args[0].MemberName})";
        }

        public override string JsonField(MethodCallExpressionModel model)
        {
            if (model.Args == null || model.Args.Count < 2)
            {
                return Unsupported("JSON 字段访问");
            }

            var path = BuildJsonPath(model);
            foreach (var argument in model.Args.Skip(1))
            {
                RemoveConsumedParameter(model, argument);
            }
            return $"JSON_VALUE({model.Args[0].MemberName}, '{path.Replace("'", "''", StringComparison.Ordinal)}')";
        }

        public override string JsonIndex(MethodCallExpressionModel model)
        {
            if (model.Args == null || model.Args.Count != 2 || !TryGetJsonIndex(model.Args[1].MemberValue, out var index))
            {
                return Unsupported("动态 JSON 数组下标");
            }

            RemoveConsumedParameter(model, model.Args[1]);
            return $"JSON_VALUE({model.Args[0].MemberName}, '$[{index}]')";
        }

        public override string RowNumber(MethodCallExpressionModel model)
        {
            return Unsupported("窗口函数");
        }

        public override string RowCount(MethodCallExpressionModel model)
        {
            return Unsupported("窗口函数");
        }

        public override string DateDiff(MethodCallExpressionModel model)
        {
            return Unsupported("日期差值");
        }

        public override string Between(MethodCallExpressionModel model)
        {
            return Unsupported("BETWEEN 条件");
        }

        public override string GetDateString(string dateValue, string formatString)
        {
            return Unsupported("日期格式化");
        }

        public override string GetStringJoinSelector(string result, string separator)
        {
            return Unsupported("字符串聚合");
        }

        public override string ToInt32(MethodCallExpressionModel model)
        {
            return Unsupported("数值类型转换");
        }

        public override string ToInt64(MethodCallExpressionModel model)
        {
            return Unsupported("数值类型转换");
        }

        public override string ToGuid(MethodCallExpressionModel model)
        {
            return Unsupported("GUID 类型转换");
        }

        public override string ToDouble(MethodCallExpressionModel model)
        {
            return Unsupported("数值类型转换");
        }

        public override string ToBool(MethodCallExpressionModel model)
        {
            return Unsupported("布尔类型转换");
        }

        public override string ToTime(MethodCallExpressionModel model)
        {
            return Unsupported("时间类型转换");
        }

        public override string ToDecimal(MethodCallExpressionModel model)
        {
            return Unsupported("数值类型转换");
        }

        public override string Trim(MethodCallExpressionModel model)
        {
            return Unsupported("去除字符串首尾空白");
        }

        public override string Substring(MethodCallExpressionModel model)
        {
            return Unsupported("截取字符串");
        }

        public override string Length(MethodCallExpressionModel model)
        {
            return Unsupported("获取字符串长度");
        }

        public override string Replace(MethodCallExpressionModel model)
        {
            return Unsupported("替换字符串内容");
        }

        public override string CharIndex(MethodCallExpressionModel model)
        {
            return Unsupported("查找字符串位置");
        }

        public override string CharIndexNew(MethodCallExpressionModel model)
        {
            return Unsupported("查找字符串位置");
        }

        public override string GetRandom()
        {
            return Unsupported("随机函数");
        }

        public override string NewUid(MethodCallExpressionModel model)
        {
            return Unsupported("UUID 生成");
        }

        public override string WeekOfYear(MethodCallExpressionModel model)
        {
            return Unsupported("周序号");
        }

        public override string TrimEnd(MethodCallExpressionModel model)
        {
            return Unsupported("去除字符串末尾空白");
        }

        public override string TrimStart(MethodCallExpressionModel model)
        {
            return Unsupported("去除字符串开头空白");
        }

        public override string Left(MethodCallExpressionModel model)
        {
            return Unsupported("字符串左侧截取");
        }

        public override string Right(MethodCallExpressionModel model)
        {
            return Unsupported("字符串右侧截取");
        }

        public override string PadLeft(MethodCallExpressionModel model)
        {
            return Unsupported("字符串左侧填充");
        }

        public override string Floor(MethodCallExpressionModel model)
        {
            return Unsupported("向下取整");
        }

        public override string Ceil(MethodCallExpressionModel model)
        {
            return Unsupported("向上取整");
        }

        public override string Stuff(MethodCallExpressionModel model)
        {
            return Unsupported("指定位置的字符串替换");
        }

        public override string Format(MethodCallExpressionModel model)
        {
            return Unsupported("字符串格式化");
        }

        public override string FormatRowNumber(MethodCallExpressionModel model)
        {
            return Unsupported("窗口函数格式化");
        }

        public override string GetForXmlPath()
        {
            return Unsupported("XML PATH 聚合");
        }

        public override string JsonContainsFieldName(MethodCallExpressionModel model)
        {
            return Unsupported("JSON 字段存在性判断");
        }

        public override string JsonArrayLength(MethodCallExpressionModel model)
        {
            return Unsupported("JSON 数组长度");
        }

        public override string JsonParse(MethodCallExpressionModel model)
        {
            return Unsupported("JSON 解析");
        }

        public override string JsonLike(MethodCallExpressionModel model)
        {
            return Unsupported("JSON 模糊匹配");
        }

        public override string JsonArrayAny(MethodCallExpressionModel model)
        {
            return Unsupported("JSON 数组包含判断");
        }

        public override string JsonListObjectAny(MethodCallExpressionModel model)
        {
            return Unsupported("JSON 对象数组包含判断");
        }

        public override string PgsqlArrayContains(MethodCallExpressionModel model)
        {
            return Unsupported("数组包含判断");
        }

        public override string FullTextContains(MethodCallExpressionModel model)
        {
            return Unsupported("全文检索");
        }

        public override string Collate(MethodCallExpressionModel model)
        {
            return Unsupported("排序规则指定");
        }

        public override string GetTableWithDataBase(string dataBaseName, string tableName)
        {
            return Unsupported("跨数据库表引用");
        }

        public override string AggregateDistinctCount(MethodCallExpressionModel model)
        {
            return Unsupported("DISTINCT 聚合");
        }

        public override string AggregateDistinctSum(MethodCallExpressionModel model)
        {
            return Unsupported("DISTINCT 聚合");
        }

        public override string AggregateDistinctAvg(MethodCallExpressionModel model)
        {
            return Unsupported("DISTINCT 聚合");
        }

        string IDbMethods.RowSum(MethodCallExpressionModel model)
        {
            return Unsupported("窗口函数");
        }

        string IDbMethods.RowMin(MethodCallExpressionModel model)
        {
            return Unsupported("窗口函数");
        }

        string IDbMethods.RowMax(MethodCallExpressionModel model)
        {
            return Unsupported("窗口函数");
        }

        string IDbMethods.RowAvg(MethodCallExpressionModel model)
        {
            return Unsupported("窗口函数");
        }

        string IDbMethods.Oracle_ToDate(MethodCallExpressionModel model)
        {
            return Unsupported("日期转换");
        }

        string IDbMethods.Oracle_ToChar(MethodCallExpressionModel model)
        {
            return Unsupported("日期格式化");
        }

        string IDbMethods.SqlServer_DateDiff(MethodCallExpressionModel model)
        {
            return Unsupported("日期差值");
        }

        string IDbMethods.ToSingle(MethodCallExpressionModel model)
        {
            return Unsupported("数值类型转换");
        }

        private static string EqualDatePart(string part, object left, object right)
        {
            return $"DATE_PART('{part}', {left}) = DATE_PART('{part}', {right})";
        }

        private static string EqualDateOnlyAndParts(object left, object right, params string[] parts)
        {
            var comparisons = new List<string>
            {
                $"DATE_ONLY({left}) = DATE_ONLY({right})"
            };
            comparisons.AddRange(parts.Select(part => EqualDatePart(part, left, right)));
            return string.Join(" AND ", comparisons);
        }

        private static string GetDatePart(DateType dateType)
        {
            return dateType switch
            {
                DateType.Year => "year",
                DateType.Quarter => "quarter",
                DateType.Month => "month",
                DateType.Day => "day",
                DateType.Hour => "hour",
                DateType.Minute => "minute",
                DateType.Second => "second",
                DateType.Millisecond => "millisecond",
                DateType.Weekday => "day_of_week",
                _ => throw new NotSupportedException("SonnetDB 当前不支持该日期分量。")
            };
        }

        private static DateType GetDateType(MethodCallExpressionArgs argument)
        {
            var value = argument.MemberValue;
            if (value is DateType dateType)
            {
                return dateType;
            }
            if (value is string dateTypeName && Enum.TryParse(dateTypeName, true, out DateType parsedDateType))
            {
                return parsedDateType;
            }
            if (value != null && int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var dateTypeValue)
                && Enum.IsDefined(typeof(DateType), dateTypeValue))
            {
                return (DateType)dateTypeValue;
            }

            throw new NotSupportedException("SonnetDB 当前不支持未识别的日期分量。");
        }

        private static string BuildJsonPath(MethodCallExpressionModel model)
        {
            var firstSegment = GetJsonPathSegment(model.Args[1]);
            var path = firstSegment.StartsWith("$", StringComparison.Ordinal)
                ? firstSegment
                : "$['" + firstSegment.Replace("'", "''", StringComparison.Ordinal) + "']";

            foreach (var argument in model.Args.Skip(2))
            {
                var segment = GetJsonPathSegment(argument);
                if (segment.StartsWith("$", StringComparison.Ordinal))
                {
                    throw new NotSupportedException("SonnetDB 的 JSON 路径只能在第一个字段参数中以 '$' 开头。");
                }
                path += "['" + segment.Replace("'", "''", StringComparison.Ordinal) + "']";
            }

            try
            {
                return JsonPath.Parse(path).Text;
            }
            catch (ArgumentException exception)
            {
                throw new NotSupportedException("SonnetDB 不支持该 JSON 路径。", exception);
            }
        }

        private static string GetJsonPathSegment(MethodCallExpressionArgs argument)
        {
            if (argument.MemberValue is string value && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            throw new NotSupportedException("SonnetDB 的 JSON 路径必须是字符串常量。");
        }

        private static bool TryGetJsonIndex(object value, out int index)
        {
            switch (value)
            {
                case byte byteValue:
                    index = byteValue;
                    return true;
                case short shortValue when shortValue >= 0:
                    index = shortValue;
                    return true;
                case int intValue when intValue >= 0:
                    index = intValue;
                    return true;
                case long longValue when longValue is >= 0 and <= int.MaxValue:
                    index = (int)longValue;
                    return true;
                default:
                    index = 0;
                    return false;
            }
        }

        private static void RemoveConsumedParameter(MethodCallExpressionModel model, MethodCallExpressionArgs argument)
        {
            var parameterName = Convert.ToString(argument.MemberName, CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(parameterName))
            {
                model.Parameters?.RemoveAll(item => string.Equals(item.ParameterName, parameterName, StringComparison.OrdinalIgnoreCase));
            }
        }

        private static string Unsupported(string feature)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 " + feature + " 功能。");
        }

        private static bool TryGetBooleanValues(MethodCallExpressionModel model, out List<bool> values)
        {
            values = new List<bool>();
            if (model.Args == null || model.Args.Count < 2 || model.Args[0].MemberValue is not IEnumerable enumerable)
            {
                return false;
            }

            foreach (var item in enumerable)
            {
                if (item is not bool booleanValue)
                {
                    values.Clear();
                    return false;
                }

                values.Add(booleanValue);
            }

            return values.Count > 0;
        }
    }
}
