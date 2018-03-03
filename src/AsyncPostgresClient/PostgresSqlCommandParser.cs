using System;
using System.Collections.Generic;
using System.Text;
using Lennox.AsyncPostgresClient.Pool;

namespace Lennox.AsyncPostgresClient
{
    internal static class PostgresSqlCommandParser
    {
        // It's safe for "bit" and "unicode" ones to be 
        enum QuotesType
        {
            No,
            Normal, // 'string'
            Escape, // E'string'
            Dollar // $$foo$$ or $name$foo$name$
        }

        struct Quotes
        {
            public QuotesType Type { get; set; }
            public string Name { get; set; }
        }

        public static unsafe IReadOnlyList<string> Perform(
            IReadOnlyList<PostgresPropertySetting> settings,
            PostgresCommand command)
        {
            Argument.HasValue(nameof(settings), settings);

            var parameters  = command.Parameters;
            var sql = command.CommandText;

            if (sql == null)
            {
                return EmptyList<string>.Value;
            }

            DemandStandardSettings(settings);

            var queries = new List<string>();
            var quotes = ObjectPool<Stack<Quotes>>.Get();
            var sb = StringBuilderPool.Get(sql.Length);
            //Quotes? currentQuotes = null;

            try
            {
                var lastChar = '\0';
                var inQuotes = false;
                fixed (char* sqlPtr = sql)
                {
                    for (var i = 0; i < sql.Length; ++i)
                    {
                        var chr = sqlPtr[i];

                        switch (chr)
                        {
                            case '\'' when lastChar != '\\':
                                sb.Append(chr);
                                inQuotes = !inQuotes;
                                break;
                            case '@' when lastChar == '@' || inQuotes:
                                sb.Append(chr);
                                lastChar = '\0';
                                continue;
                            case '@':
                                var start = i + 1;

                                for (i = start; i < sql.Length; ++i)
                                {
                                    if (!char.IsLetterOrDigit(sqlPtr[i]))
                                    {
                                        break;
                                    }
                                }

                                var name = sql.Substring(start, i - start);
                                var paramIndex = parameters.IndexOf(name);

                                if (paramIndex == -1)
                                {
                                    throw new ArgumentOutOfRangeException(
                                        "parameterName", name,
                                        "Parameter inside query was not found inside parameter list.");
                                }

                                sb.Append('$');
                                sb.Append(paramIndex + 1);

                                break;
                            default:
                                sb.Append(chr);
                                break;
                        }

                        lastChar = chr;
                    }
                }

                return queries;
            }
            finally
            {
                quotes.Clear();
                StringBuilderPool.Free(ref sb);
                ObjectPool<Stack<Quotes>>.Free(ref quotes);
            }
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
