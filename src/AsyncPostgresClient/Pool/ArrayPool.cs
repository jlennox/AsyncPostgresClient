using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Lennox.AsyncPostgresClient.Pool
{
    // NOTE: All pooling code is placeholders that will be later reworked.

    // https://github.com/dotnet/coreclr/tree/master/src/mscorlib/shared/System/Buffers
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
        private static readonly IArrayPool<T> _default = InstanceDefault();

        public static IArrayPool<T> InstanceDefault()
        {
            return new AllocatingArrayPool<T>();
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

    internal interface IObjectPool<T>
        where T : class
    {
        T Get();
        void Free(ref T obj);
    }

    public struct PoolLease<T> : IDisposable
        where T : class
    {
        // Do not store a reference in the stack that's not scoped inside a
        // using otherwise using a pool object past freeing is possible.
        public T Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Volatile.Read(ref _leased);
        }

        private T _leased;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PoolLease(T leased)
        {
            _leased = leased;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            ObjectPool<T>.Default.Free(ref _leased);
        }
    }

    internal class ObjectPool<T> : IObjectPool<T>
        where T : class
    {
        public static readonly ObjectPool<T> Default = new ObjectPool<T>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Get()
        {
            return Activator.CreateInstance<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Free(ref T obj)
        {
            var exchanged = Interlocked.Exchange(ref obj, null);

            if (exchanged == null)
            {
                return;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetObject() => Default.Get();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FreeObject(ref T obj) => Default.Free(ref obj);
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

            exchanged.Position = 0;
            exchanged.SetLength(0);
        }
    }

    // https://referencesource.microsoft.com/#mscorlib/system/text/stringbuildercache.cs,a6dbe82674916ac0
    internal static class StringBuilderPool
    {
        public static StringBuilder Get()
        {
            return new StringBuilder();
        }

        public static StringBuilder Get(int capacity)
        {
            var sb = new StringBuilder();
            sb.EnsureCapacity(capacity);
            return sb;
        }

        public static void Free(ref StringBuilder sb)
        {
            var exchanged = Interlocked.Exchange(ref sb, null);

            if (exchanged == null)
            {
                return;
            }

            exchanged.Clear();
        }
    }
}
