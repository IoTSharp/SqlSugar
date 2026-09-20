using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SqlSugar.SonnetDB
{
    /// <summary>
    /// SonnetDB 的轻量事务不支持 DDL，代码优先必须直接执行架构操作。
    /// </summary>
    public class SonnetDBCodeFirst : CodeFirstProvider
    {
        private static readonly object InitLock = new object();

        public override void InitTables(Type entityType)
        {
            var oldSlaveConnections = Context.CurrentConnectionConfig.SlaveConnectionConfigs;
            Context.CurrentConnectionConfig.SlaveConnectionConfigs = null;

            try
            {
                var splitTableAttribute = entityType.GetCustomAttribute<SplitTableAttribute>();
                var mappingInfo = Context.MappingTables?.FirstOrDefault(it =>
                    it.EntityName == entityType.Name);

                // 分表服务自行管理其生命周期。
                if (splitTableAttribute != null && mappingInfo == null)
                {
                    SplitTables().InitTables(entityType);
                    return;
                }

                lock (InitLock)
                {
                    var oldTableList = CopyMappingTables();
                    try
                    {
                        var entityInfo = Context.GetEntityNoCacheInitMappingInfo(entityType);
                        if (!Context.DbMaintenance.IsAnySystemTablePermissions())
                        {
                            Check.Exception(true, "数据库优先和代码优先需要访问 SonnetDB 元数据。");
                        }

                        Check.Exception(
                            Context.IsSystemTablesConfig,
                            "请将 SqlSugarClient 的 ConnectionConfig.InitKeyType 设置为 InitKeyType.Attribute。");

                        if (Context.Ado.Transaction != null)
                        {
                            throw new NotSupportedException(
                                "SonnetDB 不支持在事务内执行 DDL。调用代码优先功能前请提交或回滚当前事务。");
                        }

                        // 轻量事务仅支持关系表数据操作，不能包装架构操作。
                        Execute(entityType, entityInfo);
                    }
                    finally
                    {
                        RestoreMappingTables(oldTableList);
                    }
                }
            }
            finally
            {
                Context.CurrentConnectionConfig.SlaveConnectionConfigs = oldSlaveConnections;
            }
        }

        public override void ExistLogic(EntityInfo entityInfo)
        {
            if (entityInfo.Columns == null || entityInfo.Columns.Count == 0 || entityInfo.IsDisabledUpdateAll)
            {
                return;
            }

            var tableName = GetTableName(entityInfo);
            var entityColumns = entityInfo.Columns
                .Where(it => !it.IsIgnore)
                .ToList();
            var databaseColumns = Context.DbMaintenance
                .GetColumnInfosByTableName(tableName, false);

            EnsurePrimaryKeyIsUnchanged(tableName, entityColumns, databaseColumns);

            foreach (var column in entityColumns.Where(it => !string.IsNullOrEmpty(it.OldDbColumnName)))
            {
                var oldColumn = databaseColumns.FirstOrDefault(it =>
                    it.DbColumnName.Equals(column.OldDbColumnName, StringComparison.OrdinalIgnoreCase));
                var newColumn = databaseColumns.FirstOrDefault(it =>
                    it.DbColumnName.Equals(column.DbColumnName, StringComparison.OrdinalIgnoreCase));

                if (oldColumn != null && newColumn == null)
                {
                    Context.DbMaintenance.RenameColumn(tableName, column.OldDbColumnName, column.DbColumnName);
                }
            }

            databaseColumns = Context.DbMaintenance.GetColumnInfosByTableName(tableName, false);
            foreach (var column in entityColumns.Where(column => !databaseColumns.Any(databaseColumn =>
                         databaseColumn.DbColumnName.Equals(column.DbColumnName, StringComparison.OrdinalIgnoreCase))))
            {
                if (column.IsPrimarykey || column.IsIdentity)
                {
                    throw new NotSupportedException(
                        "SonnetDB 代码优先功能不能在已有表中新增或修改主键、自增列。请创建替代表并迁移数据。");
                }

                Context.DbMaintenance.AddColumn(
                    tableName,
                    EntityColumnToDbColumn(entityInfo, tableName, column));
            }

            databaseColumns = Context.DbMaintenance.GetColumnInfosByTableName(tableName, false);
            if (!entityInfo.IsDisabledDelete)
            {
                foreach (var column in databaseColumns.Where(databaseColumn => !entityColumns.Any(entityColumn =>
                             entityColumn.DbColumnName.Equals(databaseColumn.DbColumnName, StringComparison.OrdinalIgnoreCase))))
                {
                    Context.DbMaintenance.DropColumn(tableName, column.DbColumnName);
                }
            }

            databaseColumns = Context.DbMaintenance.GetColumnInfosByTableName(tableName, false);
            foreach (var column in entityColumns)
            {
                var databaseColumn = databaseColumns.FirstOrDefault(it =>
                    it.DbColumnName.Equals(column.DbColumnName, StringComparison.OrdinalIgnoreCase));
                if (databaseColumn == null)
                {
                    continue;
                }

                if (column.IsPrimarykey != databaseColumn.IsPrimarykey ||
                    column.IsIdentity != databaseColumn.IsIdentity)
                {
                    throw new NotSupportedException(
                        "SonnetDB 代码优先功能不能修改已有表的主键或自增列定义。请创建替代表并迁移数据。");
                }

                var expected = EntityColumnToDbColumn(entityInfo, tableName, column);
                if (!SonnetDBSchemaSql.AreEquivalentTypes(expected.DataType, databaseColumn.DataType) ||
                    expected.IsNullable != databaseColumn.IsNullable)
                {
                    if (column.IsPrimarykey || column.IsIdentity)
                    {
                        throw new NotSupportedException(
                            "SonnetDB 不允许代码优先功能修改主键或自增列的类型和可空性。");
                    }

                    Context.DbMaintenance.UpdateColumn(tableName, expected);
                }

                if (column.DefaultValue == null)
                {
                    if (!string.IsNullOrWhiteSpace(databaseColumn.DefaultValue))
                    {
                        if (Context.DbMaintenance is not SonnetDBDbMaintenance sonnetMaintenance)
                        {
                            throw new InvalidOperationException("SonnetDB 代码优先功能无法获取默认值维护器。");
                        }

                        sonnetMaintenance.DropDefaultValue(tableName, column.DbColumnName);
                    }
                }
                else if (!SonnetDBSchemaSql.AreEquivalentDefaults(
                             databaseColumn.DefaultValue,
                             SonnetDBSchemaSql.FormatDefaultValue(column.DefaultValue, expected.DataType),
                             expected.DataType))
                {
                    Context.DbMaintenance.AddDefaultValue(tableName, column.DbColumnName, column.DefaultValue);
                }
            }
        }

        protected override DbColumnInfo EntityColumnToDbColumn(
            EntityInfo entityInfo,
            string tableName,
            EntityColumnInfo item)
        {
            var result = base.EntityColumnToDbColumn(entityInfo, tableName, item);
            if (item.IsJson)
            {
                result.DataType = "JSON";
            }

            return result;
        }

        private static void EnsurePrimaryKeyIsUnchanged(
            string tableName,
            List<EntityColumnInfo> entityColumns,
            List<DbColumnInfo> databaseColumns)
        {
            var expected = entityColumns
                .Where(it => it.IsPrimarykey)
                .Select(it => it.DbColumnName)
                .OrderBy(it => it, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var actual = databaseColumns
                .Where(it => it.IsPrimarykey)
                .Select(it => it.DbColumnName)
                .OrderBy(it => it, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (!expected.SequenceEqual(actual, StringComparer.OrdinalIgnoreCase))
            {
                throw new NotSupportedException(
                    $"SonnetDB 代码优先功能不能修改表“{tableName}”的主键。请创建替代表并迁移数据。");
            }
        }

        private MappingTableList CopyMappingTables()
        {
            var result = new MappingTableList();
            if (Context.MappingTables == null)
            {
                Context.MappingTables = new MappingTableList();
                return result;
            }

            foreach (var table in Context.MappingTables)
            {
                result.Add(table.EntityName, table.DbTableName, table.DbShortTaleName);
            }

            return result;
        }

        private void RestoreMappingTables(MappingTableList oldTableList)
        {
            Context.MappingTables.Clear();
            foreach (var table in oldTableList)
            {
                Context.MappingTables.Add(table.EntityName, table.DbTableName, table.DbShortTaleName);
            }
        }
    }
}
