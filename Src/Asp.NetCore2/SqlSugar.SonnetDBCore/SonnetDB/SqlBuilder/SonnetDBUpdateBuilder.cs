using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace SqlSugar.SonnetDB
{
    public sealed class SonnetDBUpdateBuilder : UpdateBuilder
    {
        public override ExpressionResult GetExpressionValue(Expression expression, ResolveExpressType resolveType, bool isMapping = true)
        {
            SonnetDBDmlSupport.ValidateILike(Context);
            return base.GetExpressionValue(expression, resolveType, isMapping);
        }

        protected override string TomultipleSqlString(List<IGrouping<int, DbColumnInfo>> groupList)
        {
            throw new NotSupportedException("SonnetDB 不支持 SqlSugar 批量 UPDATE。请逐行更新或使用事务。");
        }

        protected override string GetJoinUpdate(string columnsString, ref string whereString)
        {
            throw new NotSupportedException("SonnetDB 不支持带 JOIN 的 UPDATE。");
        }

        public override string ToSqlString()
        {
            var sql = base.ToSqlString();
            var replacements = SonnetDBParameterNames.RenameUnsafe(
                Parameters,
                Builder.SqlParameterKeyWord,
                "update");
            return SonnetDBParameterNames.ReplaceOutsideQuotes(sql, replacements);
        }
    }
}
