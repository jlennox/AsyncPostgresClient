using System;
using System.Collections.Generic;

namespace Lennox.AsyncPostgresClient.Tests
{
    internal static class WiresharkBuffer
    {
        public static byte[] ReadFromString(string dump)
        {
            // Example:
            // 00000000  00 00 00 4b 00 03 00 00  75 73 65 72 00 64 65 76   ...K.... user.dev
            // 00000040  73 71 6c 5f 74 65 73 74  73 00 00                  sql_test s..
            const int addressLength = 10;
            const int charsOfHexPerRow = 49;
            var bytes = new List<byte>(dump.Length / 80);
            var i = 0;

            for (; i < dump.Length; ++i)
            {
                switch (dump[i])
                {
                    case ' ':
                    case '\n':
                    case '\r':
                        continue;
                }

                break;
            }

            for (; i < dump.Length;)
            {
                // Skip spaces
                for (; i < dump.Length; ++i)
                {
                    if (dump[i] != ' ')
                    {
                        break;
                    }
                }

                i += addressLength;
                var hexCharsRead = 0;

                for (; i < dump.Length && hexCharsRead < charsOfHexPerRow;)
                {
                    var hex = dump[i].ToString() + dump[i + 1];
                    bytes.Add((byte)Convert.ToInt32(hex, 16));
                    hexCharsRead += 2;
                    i += 2;

                    // Skip spaces
                    for (; i < dump.Length && hexCharsRead < charsOfHexPerRow; ++i)
                    {
                        if (dump[i] != ' ')
                        {
                            break;
                        }

                        ++hexCharsRead;
                    }
                }

                // Find next line.
                for (; i < dump.Length; ++i)
                {
                    if (dump[i] == '\n')
                    {
                        ++i;
                        break;
                    }

                    ++hexCharsRead;
                }
            }

            return bytes.ToArray();
        }
    }
}
