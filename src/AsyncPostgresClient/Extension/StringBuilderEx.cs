using System;
using System.Collections.Generic;
using System.Text;

namespace Lennox.AsyncPostgresClient.Extension
{
    internal static class StringBuilderEx
    {
        public static string ToStringTrim(this StringBuilder sb)
        {
            var start = 0;
            var end = sb.Length - 1;
            for (; start < sb.Length && char.IsWhiteSpace(sb[start]); ++start)
            {
            }

            if (start == sb.Length)
            {
                return "";
            }

            for (; end >= 0 && char.IsWhiteSpace(sb[end]); --end)
            {
            }

            return sb.ToString(start, end - start + 1);
        }
    }
}
