using System;

namespace SqlSugar.SonnetDB
{
    internal static class SonnetDBIdentifier
    {
        internal static void ThrowIfCrossDatabaseReference(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Contains("(", StringComparison.Ordinal))
            {
                return;
            }

            var inDoubleQuote = false;
            var hasDotOutsideQuote = false;
            for (var index = 0; index < name.Length; index++)
            {
                var character = name[index];
                if (character == '"')
                {
                    if (inDoubleQuote && index + 1 < name.Length && name[index + 1] == '"')
                    {
                        index++;
                    }
                    else
                    {
                        inDoubleQuote = !inDoubleQuote;
                    }
                }
                else if (character == '.' && !inDoubleQuote)
                {
                    hasDotOutsideQuote = true;
                    break;
                }
            }

            if (hasDotOutsideQuote)
            {
                throw new NotSupportedException(
                    "SonnetDB 当前不支持跨数据库表引用。请使用当前连接数据库中的单个表名，或为其他数据库创建独立连接。");
            }
        }
    }
}
