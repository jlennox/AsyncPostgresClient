using System.Runtime.CompilerServices;

namespace Lennox.AsyncPostgresClient.Extension
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