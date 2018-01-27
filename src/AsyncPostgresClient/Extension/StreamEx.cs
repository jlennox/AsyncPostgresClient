using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Lennox.AsyncPostgresClient.Extension
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
        public static ValueTask<int> ReadAsync(
            this Stream stream, bool async,
            byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            if (!async)
            {
                var nread = stream.Read(buffer, offset, count);
                return new ValueTask<int>(nread);
            }

            var readTask = stream.ReadAsync(
                buffer, offset, count, cancellationToken);

            return new ValueTask<int>(readTask);
        }
    }
}
