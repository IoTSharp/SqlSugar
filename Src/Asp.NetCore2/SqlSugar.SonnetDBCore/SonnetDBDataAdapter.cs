using System;
using System.Data;
using SonnetDB.Data;

namespace SqlSugar
{
    /// <summary>
    /// SonnetDB 仅公开 <see cref="DbDataReader"/>，此适配器补齐 SqlSugar 所需的查询填充接口。
    /// </summary>
    public sealed class SonnetDBDataAdapter : IDataAdapter
    {
        public SndbCommand? SelectCommand { get; set; }

        public void Fill(DataTable table)
        {
            if (table == null)
            {
                throw new ArgumentNullException(nameof(table), "DataTable 不能为空。");
            }

            using var reader = GetSelectCommand().ExecuteReader();
            AddResultSet(table, reader);
            table.AcceptChanges();
        }

        public void Fill(DataSet dataSet)
        {
            if (dataSet == null)
            {
                throw new ArgumentNullException(nameof(dataSet), "DataSet 不能为空。");
            }

            using var reader = GetSelectCommand().ExecuteReader();
            var table = new DataTable();
            AddResultSet(table, reader);
            table.AcceptChanges();
            dataSet.Tables.Add(table);
        }

        private static void AddResultSet(DataTable table, IDataReader reader)
        {
            for (var index = 0; index < reader.FieldCount; index++)
            {
                var name = reader.GetName(index).Trim();
                if (table.Columns.Contains(name))
                {
                    name += index;
                }

                table.Columns.Add(name, reader.GetFieldType(index));
            }

            while (reader.Read())
            {
                var row = table.NewRow();
                for (var index = 0; index < reader.FieldCount; index++)
                {
                    row[index] = reader.IsDBNull(index) ? DBNull.Value : reader.GetValue(index);
                }

                table.Rows.Add(row);
            }
        }

        private SndbCommand GetSelectCommand()
        {
            return SelectCommand ?? throw new InvalidOperationException("执行填充操作前必须设置查询命令。");
        }
    }
}
