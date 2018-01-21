﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncPostgresClient.Extension
{
    internal static class StreamEx
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task WriteAsync(
            this Stream stream, bool async,
            byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            if (!async)
            {
                stream.Write(buffer, offset, count);
                return Task.CompletedTask;
            }

            return stream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<int> ReadAsync(
            this Stream stream, bool async,
            byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            if (!async)
            {
                var nread = stream.Read(buffer, offset, count);
                return new ValueTask<int>(nread).AsTask();
            }

            return stream.ReadAsync(buffer, offset, count, cancellationToken);
        }
    }
}
