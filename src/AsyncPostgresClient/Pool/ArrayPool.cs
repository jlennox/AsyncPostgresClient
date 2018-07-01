using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Microsoft.IO;

namespace Lennox.AsyncPostgresClient.Pool
{
    // NOTE: A lot of pooling code is placeholders that will be later reworked.

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

    internal class SystemBufferArrayPool<T> : IArrayPool<T>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T[] Get(int size)
        {
            return System.Buffers.ArrayPool<T>.Shared.Rent(size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Free(ref T[] array)
        {
            var exchanged = Interlocked.Exchange(ref array, null);

            if (exchanged == null || exchanged.Length == 0)
            {
                return;
            }

            System.Buffers.ArrayPool<T>.Shared.Return(exchanged);
        }
    }

    // A 'dumb' placeholder to be later replaced.
    internal class AllocatingArrayPool<T> : IArrayPool<T>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T[] Get(int size)
        {
            if (size == 0)
            {
                return EmptyArray<T>.Value;
            }

            return new T[size];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            return new SystemBufferArrayPool<T>();
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

    internal interface IObjectPoolSized<T> : IObjectPool<T>
        where T : class
    {
        T Get(int size);
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
        public static T GetObject()
        {
            return Default.Get();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FreeObject(ref T obj)
        {
            Default.Free(ref obj);
        }
    }

    internal static class MemoryStreamPool
    {
        private static readonly RecyclableMemoryStreamManager _manager =
            new RecyclableMemoryStreamManager();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MemoryStream Get()
        {
            return _manager.GetStream();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MemoryStream Get(int size)
        {
            return _manager.GetStream("", size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Free(ref MemoryStream ms)
        {
            ms.Dispose();
        }
    }

    internal static class AllocatingMemoryStreamPool
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MemoryStream Get()
        {
            return new MemoryStream();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    internal static class StringBuilderPool
    {
        public static readonly IObjectPoolSized<StringBuilder> Default =
            new StringBuilderCachePool();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StringBuilder Get()
        {
            return Default.Get();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StringBuilder Get(int capacity)
        {
            return Default.Get(capacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Free(ref StringBuilder sb)
        {
            Default.Free(ref sb);
        }
    }

    internal class StringBuilderCachePool : IObjectPoolSized<StringBuilder>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder Get()
        {
            return StringBuilderCache.Acquire();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder Get(int capacity)
        {
            return StringBuilderCache.Acquire(capacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Free(ref StringBuilder sb)
        {
            var exchanged = Interlocked.Exchange(ref sb, null);

            if (exchanged == null)
            {
                return;
            }

            StringBuilderCache.Release(exchanged);
        }
    }

    internal class AllocatingStringBuilderPool : IObjectPoolSized<StringBuilder>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder Get()
        {
            return new StringBuilder();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder Get(int capacity)
        {
            var sb = new StringBuilder();
            sb.EnsureCapacity(capacity);
            return sb;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Free(ref StringBuilder sb)
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