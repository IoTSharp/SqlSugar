using System;
using System.Linq;

namespace SqlSugar.SonnetDB
{
    public class SonnetDBCodeFirst : CodeFirstProvider
    {
        public override void ExistLogic(EntityInfo entityInfo)
        {
            // Native STRING/JSON/BLOB columns do not have a declared length.
            foreach (var column in entityInfo.Columns.Where(it => !it.IsIgnore))
            {
                if (IsUnbounded(column))
                {
                    column.Length = 0;
                }
            }

            base.ExistLogic(entityInfo);
        }

        protected override DbColumnInfo EntityColumnToDbColumn(
            EntityInfo entityInfo, string tableName, EntityColumnInfo item)
        {
            var column = base.EntityColumnToDbColumn(entityInfo, tableName, item);
            if (item.IsJson)
            {
                column.DataType = "JSON";
            }
            if (item.UnderType?.Name == "DateOnly" &&
                string.Equals(column.DataType, "Date", StringComparison.OrdinalIgnoreCase))
            {
                column.DataType = "DATETIME";
            }
            if (IsUnbounded(item))
            {
                column.Length = 0;
            }

            return column;
        }

        protected override bool IsNoSamgeType(EntityColumnInfo entityColumn, DbColumnInfo databaseColumn)
        {
            if (entityColumn.IsJson)
            {
                return !string.Equals(databaseColumn.DataType, "JSON", StringComparison.OrdinalIgnoreCase);
            }
            if (entityColumn.UnderType?.Name == "DateOnly" &&
                string.Equals(entityColumn.DataType, "Date", StringComparison.OrdinalIgnoreCase))
            {
                return !string.Equals(databaseColumn.DataType, "DATETIME", StringComparison.OrdinalIgnoreCase);
            }
            if (entityColumn.UnderType?.Name == "TimeOnly" &&
                string.Equals(entityColumn.DataType, "Time", StringComparison.OrdinalIgnoreCase))
            {
                return !string.Equals(databaseColumn.DataType, "TIME", StringComparison.OrdinalIgnoreCase);
            }

            return base.IsNoSamgeType(entityColumn, databaseColumn);
        }

        private static bool IsUnbounded(EntityColumnInfo column)
        {
            if (column.IsJson)
            {
                return true;
            }

            return string.IsNullOrEmpty(column.DataType)
                ? column.UnderType == typeof(string) || column.UnderType == typeof(byte[]) || column.IsJson
                : column.DataType.Equals("STRING", StringComparison.OrdinalIgnoreCase) ||
                  column.DataType.Equals("JSON", StringComparison.OrdinalIgnoreCase) ||
                  column.DataType.Equals("BLOB", StringComparison.OrdinalIgnoreCase);
        }
    }
}
