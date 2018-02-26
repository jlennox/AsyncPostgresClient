using System;
using System.Collections.Generic;
using System.Text;
using Lennox.AsyncPostgresClient.Pool;

namespace Lennox.AsyncPostgresClient
{
    internal class PostgresSqlCommandRewriter
    {
        public static unsafe string Perform(PostgresCommand command)
        {
            var parameters  = command.Parameters;
            var sql = command.CommandText;

            if (sql == null)
            {
                return "";
            }

            var sb = StringBuilderPool.Get(sql.Length);
            
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

                return sb.ToString();
            }
            finally
            {
                StringBuilderPool.Free(ref sb);
            }
        }
    }
}
