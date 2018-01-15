using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace AsyncPostgresClient.Extension
{
    internal static class ArrayEx
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LengthOrZero<T>(this T[] array)
        {
            return array == null ? 0 : array.Length;
        }
    }
}