using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlSugar.SonnetDB
{
    public sealed class SonnetDBDbBind : DbBindProvider
    {
        public static readonly List<KeyValuePair<string, CSharpDataType>> MappingTypesConst =
            new List<KeyValuePair<string, CSharpDataType>>
            {
                new KeyValuePair<string, CSharpDataType>("INT", CSharpDataType.@long),
                new KeyValuePair<string, CSharpDataType>("FLOAT", CSharpDataType.@double),
                new KeyValuePair<string, CSharpDataType>("BOOL", CSharpDataType.@bool),
                new KeyValuePair<string, CSharpDataType>("STRING", CSharpDataType.@string),
                new KeyValuePair<string, CSharpDataType>("DATETIME", CSharpDataType.DateTime),
                new KeyValuePair<string, CSharpDataType>("BLOB", CSharpDataType.byteArray),
                new KeyValuePair<string, CSharpDataType>("JSON", CSharpDataType.@string)
            };

        public override List<KeyValuePair<string, CSharpDataType>> MappingTypes
        {
            get
            {
                var externalMappings = Context.CurrentConnectionConfig.ConfigureExternalServices?.AppendDataReaderTypeMappings;
                return externalMappings != null && externalMappings.Count > 0
                    ? externalMappings.Union(MappingTypesConst).ToList()
                    : MappingTypesConst;
            }
        }

        public override string GetDbTypeName(string csharpTypeName)
        {
            return csharpTypeName switch
            {
                "Byte[]" => "BLOB",
                "Int16" or "Int32" or "Int64" or "Byte" or "SByte" or "UInt16" or "UInt32" or "UInt64" => "INT",
                "Single" or "Double" => "FLOAT",
                "Decimal" => throw new NotSupportedException(
                    "SonnetDB 关系表不支持精确的 DECIMAL/NUMERIC 列。请使用 double 或 float，或改用 STRING 并自行处理精度。"),
                "Boolean" => "BOOL",
                "DateTime" or "DateTimeOffset" or "DateOnly" => "DATETIME",
                "TimeOnly" or "TimeSpan" => throw new NotSupportedException(
                    "SonnetDB 关系表不支持 TimeOnly 或 TimeSpan 列。请使用 DateTime 或 STRING 表示时间值。"),
                "Guid" => "STRING",
                _ => "STRING"
            };
        }

        public override string GetPropertyTypeName(string dbTypeName)
        {
            if (string.IsNullOrWhiteSpace(dbTypeName))
            {
                return "string";
            }

            var normalized = dbTypeName.Trim().ToUpperInvariant();
            if (normalized == "BLOB")
            {
                return "byte[]";
            }

            if (normalized is "STRING" or "JSON")
            {
                return "string";
            }

            var mapping = MappingTypes.FirstOrDefault(item => item.Key.Equals(normalized, StringComparison.OrdinalIgnoreCase));
            return mapping.Key == null ? "object" : mapping.Value.ToString();
        }

        public override List<string> StringThrow => new List<string>
        {
            "int32", "int64", "datetime", "decimal", "double", "byte"
        };
    }
}
