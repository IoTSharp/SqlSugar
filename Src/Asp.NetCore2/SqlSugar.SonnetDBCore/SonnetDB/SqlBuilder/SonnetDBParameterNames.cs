using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SqlSugar.SonnetDB
{
    internal static class SonnetDBParameterNames
    {
        internal static Dictionary<string, string> RenameUnsafe(
            IEnumerable<SugarParameter> parameters,
            string parameterPrefix,
            string category)
        {
            var parameterList = parameters?.ToList() ?? new List<SugarParameter>();
            var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var usedNames = new HashSet<string>(
                parameterList
                    .Where(parameter => !string.IsNullOrEmpty(parameter.ParameterName))
                    .Select(parameter => parameter.ParameterName),
                StringComparer.OrdinalIgnoreCase);
            var index = 0;

            foreach (var parameter in parameterList)
            {
                if (string.IsNullOrEmpty(parameter.ParameterName))
                {
                    throw new ArgumentException("SonnetDB 参数必须具有名称。", nameof(parameters));
                }

                if (IsSafe(parameter.ParameterName))
                {
                    continue;
                }

                if (!replacements.TryGetValue(parameter.ParameterName, out var safeName))
                {
                    var prefix = parameter.ParameterName[0] is '@' or ':'
                        ? parameter.ParameterName.Substring(0, 1)
                        : parameterPrefix;
                    do
                    {
                        safeName = prefix + "sonnet_" + category + "_" + index++;
                    }
                    while (usedNames.Contains(safeName));

                    replacements[parameter.ParameterName] = safeName;
                    usedNames.Add(safeName);
                }

                parameter.ParameterName = safeName;
            }

            return replacements;
        }

        internal static bool IsSafe(string parameterName)
        {
            return !string.IsNullOrEmpty(parameterName)
                && (parameterName[0] == '@' || parameterName[0] == ':')
                && parameterName.Length > 1
                && parameterName.Skip(1).All(character =>
                    character is >= 'A' and <= 'Z'
                    or >= 'a' and <= 'z'
                    or >= '0' and <= '9'
                    or '_');
        }

        internal static string ReplaceOutsideQuotes(
            string sql,
            IReadOnlyDictionary<string, string> replacements)
        {
            if (string.IsNullOrEmpty(sql) || replacements == null || replacements.Count == 0)
            {
                return sql;
            }

            var ordered = replacements
                .OrderByDescending(item => item.Key.Length)
                .ToArray();
            var result = new StringBuilder(sql.Length);
            var inSingleQuote = false;
            var inDoubleQuote = false;
            var inLineComment = false;
            var inBlockComment = false;

            for (var index = 0; index < sql.Length; index++)
            {
                var character = sql[index];
                var next = index + 1 < sql.Length ? sql[index + 1] : '\0';

                if (inLineComment)
                {
                    result.Append(character);
                    if (character == '\r' || character == '\n')
                    {
                        inLineComment = false;
                    }
                    continue;
                }

                if (inBlockComment)
                {
                    result.Append(character);
                    if (character == '*' && next == '/')
                    {
                        result.Append(next);
                        index++;
                        inBlockComment = false;
                    }
                    continue;
                }

                if (!inSingleQuote && !inDoubleQuote && character == '-' && next == '-')
                {
                    result.Append(character).Append(next);
                    index++;
                    inLineComment = true;
                    continue;
                }

                if (!inSingleQuote && !inDoubleQuote && character == '/' && next == '*')
                {
                    result.Append(character).Append(next);
                    index++;
                    inBlockComment = true;
                    continue;
                }

                if (character == '\'' && !inDoubleQuote)
                {
                    result.Append(character);
                    if (inSingleQuote && next == '\'')
                    {
                        result.Append(next);
                        index++;
                    }
                    else
                    {
                        inSingleQuote = !inSingleQuote;
                    }
                    continue;
                }

                if (character == '"' && !inSingleQuote)
                {
                    result.Append(character);
                    if (inDoubleQuote && next == '"')
                    {
                        result.Append(next);
                        index++;
                    }
                    else
                    {
                        inDoubleQuote = !inDoubleQuote;
                    }
                    continue;
                }

                if (!inSingleQuote && !inDoubleQuote)
                {
                    var replacement = ordered.FirstOrDefault(item =>
                        index + item.Key.Length <= sql.Length &&
                        string.Compare(sql, index, item.Key, 0, item.Key.Length, StringComparison.OrdinalIgnoreCase) == 0 &&
                        IsTokenBoundary(sql, index, item.Key.Length));
                    if (replacement.Key != null)
                    {
                        result.Append(replacement.Value);
                        index += replacement.Key.Length - 1;
                        continue;
                    }
                }

                result.Append(character);
            }

            return result.ToString();
        }

        private static bool IsTokenBoundary(string sql, int index, int length)
        {
            var before = index == 0 ? '\0' : sql[index - 1];
            var afterIndex = index + length;
            var after = afterIndex >= sql.Length ? '\0' : sql[afterIndex];
            return !IsParameterCharacter(before) && !IsParameterCharacter(after);
        }

        private static bool IsParameterCharacter(char character)
        {
            return character is >= 'A' and <= 'Z'
                or >= 'a' and <= 'z'
                or >= '0' and <= '9'
                or '_';
        }
    }
}
