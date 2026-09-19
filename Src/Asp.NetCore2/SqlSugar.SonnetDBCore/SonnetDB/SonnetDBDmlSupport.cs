using System;

namespace SqlSugar.SonnetDB
{
    internal static class SonnetDBDmlSupport
    {
        internal static void ValidateWith(string lockString)
        {
            if (!string.IsNullOrWhiteSpace(lockString)
                && !string.Equals(lockString, SqlWith.Null, StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException("SonnetDB 当前不支持 SqlSugar 的 With 表提示或锁提示语义。");
            }
        }

        internal static void ValidateILike(SqlSugarProvider context)
        {
            if (context.CurrentConnectionConfig.MoreSettings?.EnableILike == true)
            {
                throw new NotSupportedException("SonnetDB 当前不支持 MoreSettings.EnableILike。不区分大小写匹配请在应用层处理。");
            }
        }
    }
}
