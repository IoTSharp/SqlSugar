using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace SqlSugar.SonnetDB
{
    public class SonnetDBInsertable<T> : InsertableProvider<T>, IInsertable<T> where T : class, new()
    {
        IInsertable<T> IInsertable<T>.With(string lockString)
        {
            SonnetDBDmlSupport.ValidateWith(lockString);
            return this;
        }

        public override int ExecuteReturnIdentity()
        {
            EnsureSingleEntityForIdentityReturn();
            InsertBuilder.IsReturnIdentity = true;
            PreToSql();
            string identityColumn = GetIdentityColumn();
            string sql = InsertBuilder.ToSqlString().Replace("$PrimaryKey", this.SqlBuilder.GetTranslationColumnName(identityColumn));
            RestoreMapping();
            var result = GetIdentityValue(Ado.GetScalar(sql, InsertBuilder.Parameters == null ? null : InsertBuilder.Parameters.ToArray()));
            After(sql, result);
            return result;
        }
        public override async Task<int> ExecuteReturnIdentityAsync()
        {
            EnsureSingleEntityForIdentityReturn();
            InsertBuilder.IsReturnIdentity = true;
            PreToSql();
            string identityColumn = GetIdentityColumn();
            string sql = InsertBuilder.ToSqlString().Replace("$PrimaryKey", this.SqlBuilder.GetTranslationColumnName(identityColumn));
            RestoreMapping();
            var obj = await Ado.GetScalarAsync(sql, InsertBuilder.Parameters == null ? null : InsertBuilder.Parameters.ToArray());
            var result = GetIdentityValue(obj);
            After(sql, result);
            return result;
        }
        public override KeyValuePair<string, List<SugarParameter>> ToSql()
        {
            var result= base.ToSql();
            var primaryKey = GetPrimaryKeys().FirstOrDefault();
            if (primaryKey != null)
            {
                primaryKey = this.SqlBuilder.GetTranslationColumnName(primaryKey);
            }
            return new KeyValuePair<string, List<SugarParameter>>(result.Key.Replace("$PrimaryKey", primaryKey), result.Value);
        }

        public override long ExecuteReturnBigIdentity()
        {
            EnsureSingleEntityForIdentityReturn();
            InsertBuilder.IsReturnIdentity = true;
            PreToSql();
            string sql = InsertBuilder.ToSqlString().Replace("$PrimaryKey", this.SqlBuilder.GetTranslationColumnName(GetIdentityKeys().FirstOrDefault()));
            RestoreMapping();
            var result = Convert.ToInt64(Ado.GetScalar(sql, InsertBuilder.Parameters == null ? null : InsertBuilder.Parameters.ToArray()) ?? "0");
            After(sql, result);
            return result;
        }
        public override async Task<long> ExecuteReturnBigIdentityAsync()
        {
            EnsureSingleEntityForIdentityReturn();
            InsertBuilder.IsReturnIdentity = true;
            PreToSql();
            string sql = InsertBuilder.ToSqlString().Replace("$PrimaryKey", this.SqlBuilder.GetTranslationColumnName(GetIdentityKeys().FirstOrDefault()));
            RestoreMapping();
            var result = Convert.ToInt64(await Ado.GetScalarAsync(sql, InsertBuilder.Parameters == null ? null : InsertBuilder.Parameters.ToArray()) ?? "0");
            After(sql, result);
            return result;
        }

        public override bool ExecuteCommandIdentityIntoEntity()
        {
            EnsureSingleEntityForIdentityReturn();
            var result = InsertObjs.First();
            var identityKeys = GetIdentityKeys();
            if (identityKeys.Count == 0) { return this.ExecuteCommand() > 0; }
            var idValue = ExecuteReturnBigIdentity();
            Check.Exception(identityKeys.Count > 1, "将自增键写回实体时不支持多个自增键。");
            var identityKey = identityKeys.First();
            object setValue = 0;
            if (idValue > int.MaxValue)
                setValue = idValue;
            else
                setValue = Convert.ToInt32(idValue);
            var propertyName = this.Context.EntityMaintenance.GetPropertyName<T>(identityKey);
            typeof(T).GetProperties().First(t => t.Name.ToUpper() == propertyName.ToUpper()).SetValue(result, setValue, null);
            return idValue > 0;
        }

        public override async Task<bool> ExecuteCommandIdentityIntoEntityAsync()
        {
            EnsureSingleEntityForIdentityReturn();
            return await base.ExecuteCommandIdentityIntoEntityAsync();
        }

        private void EnsureSingleEntityForIdentityReturn()
        {
            if (InsertObjs != null && InsertObjs.Length > 1)
            {
                throw new NotSupportedException(
                    "SonnetDB 批量插入不能返回单个自增键或将其写回实体。请改用 ExecuteReturnPkList<T>() 获取全部主键，或逐个实体插入。");
            }
        }

        private string GetIdentityColumn()
        {
            var identityColumn = GetIdentityKeys().FirstOrDefault();
            if (identityColumn == null)
            {
                var columns = this.Context.DbMaintenance.GetColumnInfosByTableName(InsertBuilder.GetTableNameString);
                identityColumn = columns.First(it => it.IsIdentity || it.IsPrimarykey).DbColumnName;
            }
            return identityColumn;
        }

        private static int GetIdentityValue(object value)
        {
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

    }

}
