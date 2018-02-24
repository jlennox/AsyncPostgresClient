using System;
using System.Collections.Generic;
using System.Text;

namespace Lennox.AsyncPostgresClient.Diagnostic
{
    public class DebugLogger
    {
        public static bool Enabled { get; set; } = false;

        public static void Log(string line)
        {
            Console.WriteLine(line);
        }

        public static void Log<T>(string format, T arg1)
        {
            Log(string.Format(format, arg1));
        }

        public static void Log<T, T2>(string format, T arg1, T2 arg2)
        {
            Log(string.Format(format, arg1, arg2));
        }

        public static void Log<T, T2, T3>(string format, T arg1, T2 arg2, T3 arg3)
        {
            Log(string.Format(format, arg1, arg2, arg3));
        }
    }
}
