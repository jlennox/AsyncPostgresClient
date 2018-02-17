using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lennox.AsyncPostgresClient.Extension
{
    internal static class MemoryStreamEx
    {
        [ThreadStatic]
        private static byte[] _tempBuffer;

        private const int _tempBufferSize = 512;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte[] InitializeBuffer()
        {
            if (_tempBuffer == null)
            {
                _tempBuffer = new byte[_tempBufferSize];
            }

            return _tempBuffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteNetwork(this MemoryStream ms, int i)
        {
            var buffer = InitializeBuffer();

            BinaryBuffer.WriteIntNetwork(buffer, 0, i);
            ms.Write(buffer, 0, 4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteNetwork(this MemoryStream ms, short i)
        {
            var buffer = InitializeBuffer();

            BinaryBuffer.WriteShortNetwork(buffer, 0, i);
            ms.Write(buffer, 0, 2);
        }

        public static int WriteNetwork(
            this MemoryStream ms, short[] array, int length)
        {
            var buffer = InitializeBuffer();

            if (array == null)
            {
                return 0;
            }

            for (var i = 0; i < length; ++i)
            {
                BinaryBuffer.WriteShortNetwork(buffer, 0, array[i]);
                ms.Write(buffer, 0, 2);
            }

            return length * 2;
        }

        public static int WriteShortNetwork<T>(
            this MemoryStream ms, int[] array, int length)
        {
            if (array == null)
            {
                return 0;
            }

            var buffer = InitializeBuffer();

            for (var i = 0; i < length; ++i)
            {
                BinaryBuffer.WriteShortNetwork(buffer, 0, (short)array[i]);
                ms.Write(buffer, 0, 2);
            }

            return length * 2;
        }

        public static int WriteNetwork(
            this MemoryStream ms, byte[] array, int length)
        {
            if (array == null)
            {
                return 0;
            }

            ms.Write(array, 0, length);

            return length;
        }

        public static int WriteNetwork(
            this MemoryStream ms, int[] array, int length)
        {
            if (array == null)
            {
                return 0;
            }

            var buffer = InitializeBuffer();

            for (var i = 0; i < length; ++i)
            {
                BinaryBuffer.WriteIntNetwork(buffer, 0, (short)array[i]);
                ms.Write(buffer, 0, 2);
            }

            return length * 4;
        }

        public static unsafe int WriteString(
            this MemoryStream ms, string s, Encoding encoding)
        {
            var buffer = InitializeBuffer();

            if (string.IsNullOrEmpty(s))
            {
                ms.WriteByte(0);
                return 1;
            }

            if (encoding.GetMaxByteCount(s.Length) < _tempBufferSize)
            {
                var byteCount = encoding.GetBytes(
                    s, 0, s.Length, buffer, 0);

                ms.Write(buffer, 0, byteCount);
                ms.WriteByte(0);
                return byteCount + 1;
            }

            // TODO: This allocation is not pretty. Consider caching and
            // flushing.
            var encoder = encoding.GetEncoder();
            var charIndex = 0;
            var completed = false;
            var totalBytes = 0;

            fixed (char* sPtr = s)
            fixed (byte* bufPtr = buffer)
            while (!completed)
            {
                encoder.Convert(&sPtr[charIndex], s.Length - charIndex,
                    bufPtr, buffer.Length, false,
                    out var charsUsed, out var bytesUsed, out completed);

                charIndex += charsUsed;
                totalBytes += bytesUsed;

                ms.Write(buffer, 0, bytesUsed);
            }

            ms.WriteByte(0);
            return totalBytes + 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task CopyToAsync(
            this MemoryStream ms, bool async,
            Stream stream, CancellationToken cancel)
        {
            // The .net code shouldn't be allocating anything regardless
            // becuase it uses the internal byte buffer for the memorystream.
            const int bufferSize = 8 * 1024;

            if (!async)
            {
                ms.CopyTo(stream, bufferSize);
                return Task.CompletedTask;
            }

            return ms.CopyToAsync(stream, bufferSize, cancel);
        }
    }
}