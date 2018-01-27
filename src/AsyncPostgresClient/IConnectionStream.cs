using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Lennox.AsyncPostgresClient.Extension;

namespace Lennox.AsyncPostgresClient
{
    public interface IConnectionStream<T>
    {
        T CreateTcpStream(string hostname, int port);

        int Receive(T stream, byte[] buffer, int offset, int count);

        Task<int> ReceiveAsync(T stream, byte[] buffer, int offset, int count,
            CancellationToken cancellationToken);

        void Send(T stream, byte[] buffer, int offset, int count);

        Task SendAsync(T stream, byte[] buffer, int offset, int count,
            CancellationToken cancellationToken);

        void Dispose(T stream);
    }

    internal static class ConnectionStreamEx
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<int> Receive<T>(
            this IConnectionStream<T> connectionStream,
            bool async, T stream, byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            if (!async)
            {
                var nread = connectionStream.Receive(
                    stream, buffer, offset, count);
                return new ValueTask<int>(nread);
            }

            var ReceiveTask = connectionStream.ReceiveAsync(
                stream, buffer, offset, count, cancellationToken);

            return new ValueTask<int>(ReceiveTask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task Send<T>(
            this IConnectionStream<T> connectionStream,
            bool async, T stream, byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            if (!async)
            {
                connectionStream.Send(stream, buffer, offset, count);
                return Task.CompletedTask;
            }

            return connectionStream.SendAsync(
                stream, buffer, offset, count, cancellationToken);
        }
    }

    public struct ClrClient : IDisposable
    {
        public TcpClient Client { get; }
        public Socket Socket { get; }

        internal ClrClient(TcpClient client)
        {
            Client = client;
            Socket = client.Client;
        }

        public void Dispose()
        {
            Client.TryDispose();
            Socket.TryDispose();
        }
    }

    public class ClrConnectionStream : IConnectionStream<ClrClient>
    {
        public static readonly ClrConnectionStream Default =
            new ClrConnectionStream();

        public ClrClient CreateTcpStream(string hostname, int port)
        {
            var client = new TcpClient(hostname, port);

            return new ClrClient(client);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Receive(ClrClient stream,
            byte[] buffer, int offset, int count)
        {
            return stream.Socket
                .Receive(buffer, offset, count, SocketFlags.None);
        }

        // TODO: Add cancellation support.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<int> ReceiveAsync(
            ClrClient stream, byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            var segment = new ArraySegment<byte>(buffer, offset, count);
            return stream.Socket.ReceiveAsync(segment, SocketFlags.None);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Send(ClrClient stream, byte[] buffer, int offset, int count)
        {
            stream.Socket.Send(buffer, offset, count, SocketFlags.None);
        }

        // TODO: Add cancellation support.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task SendAsync(
            ClrClient stream, byte[] buffer, int offset,
            int count, CancellationToken cancellationToken)
        {
            var segment = new ArraySegment<byte>(buffer, offset, count);
            return stream.Socket.SendAsync(segment, SocketFlags.None);
        }

        public void Dispose(ClrClient stream)
        {
            stream.Dispose();
        }
    }
}
