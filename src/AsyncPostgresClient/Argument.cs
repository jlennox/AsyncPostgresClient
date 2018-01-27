using System;
using System.Runtime.CompilerServices;

namespace Lennox.AsyncPostgresClient
{
    internal static class Argument
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void HasValue<T>(string name, T value)
            where T : class
        {
            if (value == null)
            {
                throw new ArgumentNullException(name);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void HasValue<T>(string name, T value, string message)
            where T : class
        {
            if (value == null)
            {
                throw new ArgumentNullException(name, message);
            }
        }
    }
}
