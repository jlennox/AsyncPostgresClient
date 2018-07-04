using System;
using System.Collections.Generic;
using Lennox.AsyncPostgresClient.Extension;
using Lennox.AsyncPostgresClient.Pool;

namespace Lennox.AsyncPostgresClient
{
    internal static class PostgresSqlCommandParser
    {
        public static unsafe IReadOnlyList<string> Perform(
            IReadOnlyList<PostgresPropertySetting> settings,
            PostgresCommand command)
        {
            var parameters  = command.Parameters;
            var sql = command.CommandText;

            if (sql == null)
            {
                return EmptyList<string>.Value;
            }

            DemandStandardSettings(settings);

            var queries = new List<string>();
            var sb = StringBuilderPool.Get(sql.Length);
            var lastChar = '\0';

            fixed (char* sqlPtr = sql)
            for (var i = 0; i < sql.Length; ++i)
            {
                var chr = sqlPtr[i];
                var nextChr = i == sql.Length - 1 ? '\0' : sqlPtr[i + 1];

                switch (chr)
                {
                    // Handle strings made with quotes, 'foo' and E'foo'
                    case '\'':
                        var escapedQuotes = lastChar == 'E';

                        sb.Append(chr);
                        lastChar = '\0';

                        for (++i; i < sql.Length; ++i)
                        {
                            chr = sqlPtr[i];
                            sb.Append(chr);

                            // Quotes (chr == '\'') can be inside a string
                            // in several ways:
                            // * If they're doubled up.
                            // * If we're inside an escape string escaped
                            //   with a backslash.
                            if (chr == '\'' && lastChar != '\'' &&
                                !(escapedQuotes && lastChar == '\\'))
                            {
                                goto next;
                            }

                            lastChar = chr;
                        }

                        continue;
                    // Handle dollar strings, $$foo$$ and $bar$foo$bar$
                    case '$':
                        var k = i + 1;

                        // Syntax is "$abc$" or "$$", if "named" then "a"
                        // must be a letter. bc+ can be letter or digit.
                        // But "$5" is also valid syntax for parameter
                        // access, so we must respect those.

                        if (k >= sql.Length)
                        {
                            goto default;
                        }

                        var chrK = sqlPtr[k];

                        if (chrK == '$')
                        {
                            var indexOf = sql.IndexOf("$$", k,
                                StringComparison.Ordinal);

                            // Really... it's invalid syntax.
                            if (indexOf == -1)
                            {
                                goto default;
                            }

                            // 2 is length of "$$"
                            sb.Append(sql, i, indexOf - i + 2);

                            i = indexOf + 1;
                            goto next;
                        }

                        if (!char.IsLetter(chrK))
                        {
                            goto default;
                        }

                        sb.Append('$');
                        sb.Append(chrK);

                        for (++k; k < sql.Length; ++k)
                        {
                            chrK = sqlPtr[k];
                            sb.Append(chrK);
                            if (chrK == '$') break;
                            if (!char.IsLetterOrDigit(chrK)) goto default;
                        }

                        // +1 to account for final $.
                        ++k;

                        var namedStringStart = i;
                        var matchIndex = namedStringStart;
                        var matchedCount = 0;
                        var matchLength = k - namedStringStart;

                        for (i = k; i < sql.Length; ++i)
                        {
                            for (var m = i; m < sql.Length; ++m, ++matchIndex)
                            {
                                chr = sqlPtr[m];

                                sb.Append(chr);
                                lastChar = chr;

                                if (chr != sqlPtr[matchIndex])
                                {
                                    i = m;
                                    matchedCount = 0;
                                    matchIndex = namedStringStart;
                                    break;
                                }

                                if (++matchedCount == matchLength)
                                {
                                    i = m;
                                    // Match success.
                                    goto next;
                                }
                            }
                        }

                        // If we enumerate the entire string and do not
                        // find a match, it's technically invalid syntax
                        // but that's the user's problem. They're better
                        // off getting the actual error from postgres.

                        continue;
                    // Handle @@NotNamedParameter
                    case '@' when nextChr == '@':
                        // Append and fast forward past next one.
                        sb.Append(chr);
                        ++i;
                        lastChar = '\0';
                        continue;
                    // Handle @NamedParameter
                    case '@':
                        var start = i + 1;

                        var offset = 0;
                        for (i = start; i < sql.Length; ++i)
                        {
                            if (!char.IsLetterOrDigit(sqlPtr[i]))
                            {
                                --i;
                                offset = 1;
                                break;
                            }
                        }

                        var name = sql.Substring(start, i - start + offset);
                        var paramIndex = parameters.IndexOf(name);

                        if (paramIndex == -1)
                        {
                            throw new ArgumentOutOfRangeException(
                                "parameterName", name,
                                "Parameter inside query was not found inside parameter list.");
                        }

                        sb.Append('$');
                        sb.Append(paramIndex + 1);
                        lastChar = '\0';
                        continue;
                    // Handle -- quotes.
                    case '-' when lastChar == '-':
                        sb.Append(chr);
                        lastChar = '\0';

                        for (++i; i < sql.Length; ++i)
                        {
                            chr = sqlPtr[i];

                            sb.Append(chr);
                            lastChar = chr;

                            if (chr == '\n')
                            {
                                break;
                            }
                        }
                        continue;
                    // Handle /* */ quotes.
                    case '*' when lastChar == '/':
                        if (i == sql.Length - 1)
                        {
                            goto default;
                        }

                        var indexOfComment = sql.IndexOf("*/", i + 1);

                        // Really... it's invalid syntax.
                        if (indexOfComment == -1)
                        {
                            goto default;
                        }

                        // 2 is length of "*/"
                        sb.Append(sql, i, indexOfComment - i + 2);

                        i = indexOfComment + 1;
                        continue;
                    case ';':
                        var singleSqlCommand = sb.ToStringTrim();
                        sb.Clear();

                        if (!string.IsNullOrWhiteSpace(singleSqlCommand))
                        {
                            queries.Add(singleSqlCommand);
                        }

                        continue;
                    default:
                        sb.Append(chr);
                        lastChar = chr;
                        continue;
                    }

                next:;
            }

            if (sb.Length > 0)
            {
                var singleSqlCommand = sb.ToStringTrim();

                if (!string.IsNullOrWhiteSpace(singleSqlCommand))
                {
                    queries.Add(singleSqlCommand);
                }
            }

            StringBuilderPool.Free(ref sb);
            return queries;
        }

        internal static void DemandStandardSettings(
            IReadOnlyList<PostgresPropertySetting> settings)
        {
            if (settings == null)
            {
                return;
            }

            for (var i = 0; i < settings.Count; ++i)
            {
                var setting = settings[i];

                switch (setting.Name)
                {
                    case PostgresProperties.BackslashQuote:
                        if (setting.Value == "safe_encoding") continue;
                        break;
                    case PostgresProperties.StandardConformingStrings:
                        if (setting.Value == "on") continue;
                        break;
                    default:
                        continue;
                }

                throw new ArgumentOutOfRangeException(
                    setting.Name, setting.Value,
                    "Only the default setting value is supported.");
            }
        }
    }
}
