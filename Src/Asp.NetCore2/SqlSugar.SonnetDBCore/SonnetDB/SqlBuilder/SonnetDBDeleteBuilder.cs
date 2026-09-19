using System;
using System.Linq.Expressions;

namespace SqlSugar.SonnetDB
{
    public class SonnetDBDeleteBuilder : DeleteBuilder
    {
        public override ExpressionResult GetExpressionValue(Expression expression, ResolveExpressType resolveType)
        {
            SonnetDBDmlSupport.ValidateILike(Context);
            return base.GetExpressionValue(expression, resolveType);
        }

        public override string ToSqlString()
        {
            if (BigDataInValues != null && BigDataInValues.Count > 0)
            {
                throw new NotSupportedException("SonnetDB 当前不支持超过 10000 个主键的批量删除。请分批执行删除操作。");
            }

            return base.ToSqlString();
        }
    }
}
