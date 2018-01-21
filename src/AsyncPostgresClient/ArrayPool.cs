using System;
using System.Collections.Generic;
using System.IO;
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

    internal interface IArrayPool<T>
    {
        T[] Get(int size);
        void Free(ref T[] array);
    }

    // A 'dumb' placeholder to be later replaced.
    internal class AllocatingArrayPool<T> : IArrayPool<T>
    {
        public T[] Get(int size)
        {
            if (size == 0)
            {
                return EmptyArray<T>.Value;
            }

            return new T[size];
        }

        public void Free(ref T[] array)
        {
            var exchanged = Interlocked.Exchange(ref array, null);

            if (exchanged == null || exchanged.Length == 0)
            {
                return;
            }
        }
    }

    internal static class ArrayPool<T>
    {
        private static readonly IArrayPool<T> _default =
            new AllocatingArrayPool<T>();

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

    internal static class MemoryStreamPool
    {
        public static MemoryStream Get()
        {
            return new MemoryStream();
        }

        public static void Free(ref MemoryStream ms)
        {
            var exchanged = Interlocked.Exchange(ref ms, null);

            if (exchanged == null)
            {
                return;
            }
        }
    }
}
