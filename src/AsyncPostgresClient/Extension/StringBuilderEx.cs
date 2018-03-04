using System;
using System.Collections.Generic;
using System.Text;

namespace Lennox.AsyncPostgresClient.Extension
{
    internal static class StringBuilderEx
    {
        public static string ToStringTrim(this StringBuilder sb)
        {
            return sb.ToString();
        }
    }
}
