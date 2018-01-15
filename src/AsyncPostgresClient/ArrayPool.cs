using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace AsyncPostgresClient
{
    internal static class ArrayPool
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Free<T>(ref T[] array)
        {
            ArrayPool<T>.FreeArray(ref array);
        }
    }

    internal class ArrayPool<T>
    {
        private static readonly T[] _empty = new T[0];
        private static readonly ArrayPool<T> _default = new ArrayPool<T>();

        public T[] Get(int size)
        {
            if (size == 0)
            {
                return _empty;
            }

            return new T[size];
        }

        public void Free(ref T[] array)
        {
            var exchanged = Interlocked.Exchange(ref array, null);

            if (exchanged == null || exchanged.Length != 0)
            {
                return;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] GetArray(int size)
        {
            return _default.Get(size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FreeArray(ref T[] array)
        {
            _default.Free(ref array);
        }
    }
}
