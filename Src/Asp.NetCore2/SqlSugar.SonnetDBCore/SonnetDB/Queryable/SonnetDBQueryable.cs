using System;
using System.Linq.Expressions;

namespace SqlSugar.SonnetDB
{
    public class SonnetDBQueryable<T> : QueryableProvider<T>
    {
        public override ISugarQueryable<T> With(string withString)
        {
            SonnetDBQueryableSupport.ValidateWith(withString);
            return this;
        }

        public override ISugarQueryable<T> PartitionBy(string groupFileds)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        public override ISugarQueryable<T> PartitionBy(Expression<Func<T, object>> expression)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }
    }

    public class SonnetDBQueryable<T, T2> : QueryableProvider<T, T2>, ISugarQueryable<T, T2>
    {
        public new ISugarQueryable<T, T2> PartitionBy(Expression<Func<T, object>> expression)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        public new ISugarQueryable<T, T2> PartitionBy(Expression<Func<T, T2, object>> expression)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        public new ISugarQueryable<T, T2> PartitionBy(string groupFileds)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        public new ISugarQueryable<T, T2> With(string withString)
        {
            SonnetDBQueryableSupport.ValidateWith(withString);
            return this;
        }

        ISugarQueryable<T, T2> ISugarQueryable<T, T2>.With(string withString)
        {
            return With(withString);
        }
    }

    public class SonnetDBQueryable<T, T2, T3> : QueryableProvider<T, T2, T3>, ISugarQueryable<T, T2, T3>
    {
        public new ISugarQueryable<T, T2, T3> PartitionBy(Expression<Func<T, object>> expression)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        public new ISugarQueryable<T, T2, T3> PartitionBy(Expression<Func<T, T2, object>> expression)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        public new ISugarQueryable<T, T2, T3> PartitionBy(Expression<Func<T, T2, T3, object>> expression)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        public new ISugarQueryable<T, T2, T3> PartitionBy(string groupFileds)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        public new ISugarQueryable<T, T2, T3> With(string withString)
        {
            SonnetDBQueryableSupport.ValidateWith(withString);
            return this;
        }

        ISugarQueryable<T, T2, T3> ISugarQueryable<T, T2, T3>.With(string withString)
        {
            return With(withString);
        }
    }

    public class SonnetDBQueryable<T, T2, T3, T4> : QueryableProvider<T, T2, T3, T4>, ISugarQueryable<T, T2, T3, T4>
    {
        public new ISugarQueryable<T, T2, T3, T4> PartitionBy(Expression<Func<T, object>> expression)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        public new ISugarQueryable<T, T2, T3, T4> PartitionBy(string groupFileds)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        ISugarQueryable<T> ISugarQueryable<T>.PartitionBy(Expression<Func<T, object>> expression)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        ISugarQueryable<T> ISugarQueryable<T>.PartitionBy(string groupFileds)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        public new ISugarQueryable<T, T2, T3, T4> With(string withString)
        {
            SonnetDBQueryableSupport.ValidateWith(withString);
            return this;
        }

        ISugarQueryable<T, T2, T3, T4> ISugarQueryable<T, T2, T3, T4>.With(string withString)
        {
            return With(withString);
        }
    }

    public class SonnetDBQueryable<T, T2, T3, T4, T5> : QueryableProvider<T, T2, T3, T4, T5>, ISugarQueryable<T, T2, T3, T4, T5>
    {
        public new ISugarQueryable<T, T2, T3, T4, T5> PartitionBy(Expression<Func<T, object>> expression)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        public new ISugarQueryable<T, T2, T3, T4, T5> PartitionBy(string groupFileds)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        ISugarQueryable<T> ISugarQueryable<T>.PartitionBy(Expression<Func<T, object>> expression)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        ISugarQueryable<T> ISugarQueryable<T>.PartitionBy(string groupFileds)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        public new ISugarQueryable<T, T2, T3, T4, T5> With(string withString)
        {
            SonnetDBQueryableSupport.ValidateWith(withString);
            return this;
        }

        ISugarQueryable<T, T2, T3, T4, T5> ISugarQueryable<T, T2, T3, T4, T5>.With(string withString)
        {
            return With(withString);
        }
    }

    public class SonnetDBQueryable<T, T2, T3, T4, T5, T6> : QueryableProvider<T, T2, T3, T4, T5, T6>, ISugarQueryable<T, T2, T3, T4, T5, T6>
    {
        public new ISugarQueryable<T, T2, T3, T4, T5, T6> PartitionBy(Expression<Func<T, object>> expression)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        public new ISugarQueryable<T, T2, T3, T4, T5, T6> PartitionBy(string groupFileds)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        ISugarQueryable<T> ISugarQueryable<T>.PartitionBy(Expression<Func<T, object>> expression)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        ISugarQueryable<T> ISugarQueryable<T>.PartitionBy(string groupFileds)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        public new ISugarQueryable<T, T2, T3, T4, T5, T6> With(string withString)
        {
            SonnetDBQueryableSupport.ValidateWith(withString);
            return this;
        }

        ISugarQueryable<T, T2, T3, T4, T5, T6> ISugarQueryable<T, T2, T3, T4, T5, T6>.With(string withString)
        {
            return With(withString);
        }
    }

    public class SonnetDBQueryable<T, T2, T3, T4, T5, T6, T7> : QueryableProvider<T, T2, T3, T4, T5, T6, T7>, ISugarQueryable<T, T2, T3, T4, T5, T6, T7>
    {
        public new ISugarQueryable<T, T2, T3, T4, T5, T6, T7> PartitionBy(Expression<Func<T, object>> expression)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        public new ISugarQueryable<T, T2, T3, T4, T5, T6, T7> PartitionBy(string groupFileds)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        ISugarQueryable<T> ISugarQueryable<T>.PartitionBy(Expression<Func<T, object>> expression)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        ISugarQueryable<T> ISugarQueryable<T>.PartitionBy(string groupFileds)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        public new ISugarQueryable<T, T2, T3, T4, T5, T6, T7> With(string withString)
        {
            SonnetDBQueryableSupport.ValidateWith(withString);
            return this;
        }

        ISugarQueryable<T, T2, T3, T4, T5, T6, T7> ISugarQueryable<T, T2, T3, T4, T5, T6, T7>.With(string withString)
        {
            return With(withString);
        }
    }

    public class SonnetDBQueryable<T, T2, T3, T4, T5, T6, T7, T8> : QueryableProvider<T, T2, T3, T4, T5, T6, T7, T8>, ISugarQueryable<T, T2, T3, T4, T5, T6, T7, T8>
    {
        public new ISugarQueryable<T, T2, T3, T4, T5, T6, T7, T8> PartitionBy(Expression<Func<T, object>> expression)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        public new ISugarQueryable<T, T2, T3, T4, T5, T6, T7, T8> PartitionBy(string groupFileds)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        ISugarQueryable<T> ISugarQueryable<T>.PartitionBy(Expression<Func<T, object>> expression)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        ISugarQueryable<T> ISugarQueryable<T>.PartitionBy(string groupFileds)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        public new ISugarQueryable<T, T2, T3, T4, T5, T6, T7, T8> With(string withString)
        {
            SonnetDBQueryableSupport.ValidateWith(withString);
            return this;
        }

        ISugarQueryable<T, T2, T3, T4, T5, T6, T7, T8> ISugarQueryable<T, T2, T3, T4, T5, T6, T7, T8>.With(string withString)
        {
            return With(withString);
        }
    }

    public class SonnetDBQueryable<T, T2, T3, T4, T5, T6, T7, T8, T9> : QueryableProvider<T, T2, T3, T4, T5, T6, T7, T8, T9>, ISugarQueryable<T, T2, T3, T4, T5, T6, T7, T8, T9>
    {
        public new ISugarQueryable<T, T2, T3, T4, T5, T6, T7, T8, T9> PartitionBy(Expression<Func<T, object>> expression)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        public new ISugarQueryable<T, T2, T3, T4, T5, T6, T7, T8, T9> PartitionBy(string groupFileds)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        ISugarQueryable<T> ISugarQueryable<T>.PartitionBy(Expression<Func<T, object>> expression)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        ISugarQueryable<T> ISugarQueryable<T>.PartitionBy(string groupFileds)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        public new ISugarQueryable<T, T2, T3, T4, T5, T6, T7, T8, T9> With(string withString)
        {
            SonnetDBQueryableSupport.ValidateWith(withString);
            return this;
        }

        ISugarQueryable<T, T2, T3, T4, T5, T6, T7, T8, T9> ISugarQueryable<T, T2, T3, T4, T5, T6, T7, T8, T9>.With(string withString)
        {
            return With(withString);
        }
    }

    public class SonnetDBQueryable<T, T2, T3, T4, T5, T6, T7, T8, T9, T10> : QueryableProvider<T, T2, T3, T4, T5, T6, T7, T8, T9, T10>, ISugarQueryable<T, T2, T3, T4, T5, T6, T7, T8, T9, T10>
    {
        public new ISugarQueryable<T, T2, T3, T4, T5, T6, T7, T8, T9, T10> PartitionBy(Expression<Func<T, object>> expression)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        public new ISugarQueryable<T, T2, T3, T4, T5, T6, T7, T8, T9, T10> PartitionBy(string groupFileds)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        ISugarQueryable<T> ISugarQueryable<T>.PartitionBy(Expression<Func<T, object>> expression)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        ISugarQueryable<T> ISugarQueryable<T>.PartitionBy(string groupFileds)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        public new ISugarQueryable<T, T2, T3, T4, T5, T6, T7, T8, T9, T10> With(string withString)
        {
            SonnetDBQueryableSupport.ValidateWith(withString);
            return this;
        }

        ISugarQueryable<T, T2, T3, T4, T5, T6, T7, T8, T9, T10> ISugarQueryable<T, T2, T3, T4, T5, T6, T7, T8, T9, T10>.With(string withString)
        {
            return With(withString);
        }
    }

    public class SonnetDBQueryable<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> : QueryableProvider<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>, ISugarQueryable<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>
    {
        public new ISugarQueryable<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> PartitionBy(Expression<Func<T, object>> expression)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        public new ISugarQueryable<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> PartitionBy(string groupFileds)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        ISugarQueryable<T> ISugarQueryable<T>.PartitionBy(Expression<Func<T, object>> expression)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        ISugarQueryable<T> ISugarQueryable<T>.PartitionBy(string groupFileds)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        public new ISugarQueryable<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> With(string withString)
        {
            SonnetDBQueryableSupport.ValidateWith(withString);
            return this;
        }

        ISugarQueryable<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> ISugarQueryable<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>.With(string withString)
        {
            return With(withString);
        }
    }

    public class SonnetDBQueryable<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> : QueryableProvider<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>, ISugarQueryable<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>
    {
        public new ISugarQueryable<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> PartitionBy(Expression<Func<T, object>> expression)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        public new ISugarQueryable<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> PartitionBy(string groupFileds)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        ISugarQueryable<T> ISugarQueryable<T>.PartitionBy(Expression<Func<T, object>> expression)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        ISugarQueryable<T> ISugarQueryable<T>.PartitionBy(string groupFileds)
        {
            throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 PARTITION BY 分页语义。");
        }

        public new ISugarQueryable<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> With(string withString)
        {
            SonnetDBQueryableSupport.ValidateWith(withString);
            return this;
        }

        ISugarQueryable<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> ISugarQueryable<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>.With(string withString)
        {
            return With(withString);
        }
    }

    internal static class SonnetDBQueryableSupport
    {
        internal static void ValidateWith(string withString)
        {
            if (!string.IsNullOrWhiteSpace(withString)
                && !string.Equals(withString, SqlWith.Null, StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 With 表提示或 CTE 语义。");
            }
        }
    }
}
