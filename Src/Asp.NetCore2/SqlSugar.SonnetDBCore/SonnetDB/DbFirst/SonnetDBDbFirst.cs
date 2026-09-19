using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SqlSugar.SonnetDB
{
    public class SonnetDBDbFirst : DbFirstProvider
    {
        public SonnetDBDbFirst()
        {
            // SonnetDB 目录生成的代码注释统一使用简体中文。
            SettingPropertyDescriptionTemplate(template => template
                .Replace("Desc:", "说明：", StringComparison.Ordinal)
                .Replace("Default:", "默认值：", StringComparison.Ordinal)
                .Replace("Nullable:", "可空：", StringComparison.Ordinal));
        }
    }
}
