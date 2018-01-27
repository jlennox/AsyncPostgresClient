using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Lennox.AsyncPostgresClient.Extension;
using Lennox.AsyncPostgresClient.Pool;

namespace Lennox.AsyncPostgresClient
{
    public class PostgresDbConnection : PostgresDbConnection<ClrClient>
    {
        internal const int BufferSize = 16 * 1024;

        // Don't use the global array pool. This pool will contain all large
        // buffers of the same size.
        private static readonly IArrayPool<byte> _bufferPool =
            ArrayPool<byte>.InstanceDefault();

        public PostgresDbConnection(string connectionString)
            : base(connectionString, ClrConnectionStream.Default)
        {
        }

        public PostgresDbConnection(string connectionString, bool asyncOnly)
            : base(connectionString, ClrConnectionStream.Default, asyncOnly)
        {
        }

        internal static byte[] GetBuffer()
        {
            return _bufferPool.Get(BufferSize);
        }

        internal static void FreeBuffer(ref byte[] buffer)
        {
            _bufferPool.Free(ref buffer);
        }
    }

    public interface IPosgresDbConnection : IDbConnection
    {
        Task Query(bool async, string query,
            CancellationToken cancellationToken);
    }

    public class PostgresDbConnection<TStream>
        : DbConnection, IPosgresDbConnection
    {
        public override string ConnectionString { get; set; }
        public override string Database { get; }
        public override ConnectionState State { get; }
        public override string DataSource { get; }
        public override string ServerVersion { get; }

        public bool AsyncOnly { get; }

        private readonly IConnectionStream<TStream> _connectionStream;
        private readonly PostgresConnectionString _connectionString;
        private PostgresReadState _readState = new PostgresReadState();
        private PostgresClientState _clientState =
            PostgresClientState.CreateDefault();

        private TStream _stream;
        private MemoryStream _writeBuffer;
        private byte[] _buffer;
        private int _bufferOffset;
        private int _bufferCount;

        private int _isDisposed;
        private readonly object _disposeSync = new object();
        private readonly CancellationTokenSource _cancel =
            new CancellationTokenSource();

        public PostgresDbConnection(
            string connectionString,
            IConnectionStream<TStream> connectionStream,
            bool asyncOnly)
        {
            _connectionString = new PostgresConnectionString(connectionString);
            _connectionStream = connectionStream;
            _buffer = PostgresDbConnection.GetBuffer();
            _writeBuffer = MemoryStreamPool.Get();
            AsyncOnly = asyncOnly;
        }

        public PostgresDbConnection(
            string connectionString,
            IConnectionStream<TStream> connectionStream)
            : this(connectionString, connectionStream, false)
        {
        }

        protected override DbTransaction BeginDbTransaction(
            IsolationLevel isolationLevel)
        {
            throw new NotImplementedException();
        }

        public override void ChangeDatabase(string databaseName)
        {
            throw new NotImplementedException();
        }

        public override void Close()
        {
        }

        public override void Open()
        {
            OpenAsync(false, CancellationToken.None).Forget();
        }

        public override Task OpenAsync(
            CancellationToken cancellationToken)
        {
            return OpenAsync(true, cancellationToken);
        }

        private async Task OpenAsync(bool async,
            CancellationToken cancellationToken)
        {
            _stream = _connectionStream.CreateTcpStream(
                _connectionString.Hostname,
                _connectionString.Port);

            const int messageCount = 3;
            var messages = ArrayPool<KeyValuePair<string, string>>
                .GetArray(messageCount);

            try
            {
                messages[0] = new KeyValuePair<string, string>(
                    "user", _connectionString.Username);
                messages[1] = new KeyValuePair<string, string>(
                    "client_encoding", _connectionString.Encoding);
                messages[2] = new KeyValuePair<string, string>(
                    "database", _connectionString.Database);

                WriteMessage(new StartupMessage {
                    MessageCount = messageCount,
                    Messages = messages
                });

                await FlushWrites(async, cancellationToken)
                    .ConfigureAwait(false);

                var authMessageTask = EnsureNextMessage<AuthenticationMessage>(
                        async, cancellationToken);

                using (var authMessage = await authMessageTask
                    .ConfigureAwait(false))
                {
                    Authenticate(authMessage);
                }

                await FlushWrites(async, cancellationToken)
                    .ConfigureAwait(false);

                var authMessageOkTask = EnsureNextMessage<AuthenticationMessage>(
                    async, cancellationToken);

                using (var authOkMessage = await authMessageOkTask
                    .ConfigureAwait(false))
                {
                    
                }
            }
            finally
            {
                ArrayPool.Free(ref messages);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteMessage<T>(T message)
            where T : IPostgresMessage
        {
            message.Write(ref _clientState, _writeBuffer);
        }

        private async Task FlushWrites(
            bool async, CancellationToken cancellationToken)
        {
            _writeBuffer.Position = 0;

            using (var kancel = _cancel.Combine(cancellationToken))
            {
                var writeBuffer = _writeBuffer.GetBuffer();

                await Send(async, writeBuffer, 0, (int)_writeBuffer.Length,
                    kancel.Token).ConfigureAwait(false);
            }

            _writeBuffer.Position = 0;
            _writeBuffer.SetLength(0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Task Send(
            bool async, byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            return _connectionStream.Send(
                async, _stream, buffer, offset, count,
                cancellationToken);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ValueTask<int> Receive(
            bool async, byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            return _connectionStream.Receive(
                async, _stream, buffer, offset, count,
                cancellationToken);
        }

        async Task IPosgresDbConnection.Query(
            bool async, string query, CancellationToken cancellationToken)
        {
            WriteMessage(new QueryMessage {
                Query = query
            });

            await FlushWrites(async, cancellationToken).ConfigureAwait(false);
            await EnsureNextMessage<CommandCompleteMessage>(
                async, cancellationToken).ConfigureAwait(false);
            await EnsureNextMessage<ReadyForQueryMessage>(
                async, cancellationToken).ConfigureAwait(false);
        }

        private void Authenticate(AuthenticationMessage authenticationMessage)
        {
            PasswordMessage passwordMessage;
            switch (authenticationMessage.AuthenticationMessageType)
            {
                case AuthenticationMessageType.MD5Password:
                    passwordMessage = PasswordMessage.CreateMd5(
                        authenticationMessage, _clientState,
                        _connectionString);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(authenticationMessage.AuthenticationMessageType),
                        authenticationMessage.AuthenticationMessageType,
                        "Authentication method not supported.");
            }

            using (passwordMessage)
            {
                WriteMessage(passwordMessage);
            }
        }

        private async Task<T> EnsureNextMessage<T>(
            bool async, CancellationToken cancellationToken)
            where T : IPostgresMessage
        {
            var message = await ReadNextMessage(async, cancellationToken)
                .ConfigureAwait(false);

            if (!(message is T))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(T), typeof(T),
                    $"Unexpected message. Received {message.GetType()}, expected {typeof(T)}.");
            }

            return (T)message;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool PostgresMessageRead(out IPostgresMessage message)
        {
            var foundMessage = PostgresMessage.ReadMessage(
                _buffer, ref _bufferOffset, ref _bufferCount,
                ref _readState, ref _clientState, out message);

            if (foundMessage)
            {
                switch (message)
                {
                    case ErrorResponseMessage errorMessage:
                        throw new PostgresErrorException(errorMessage);
                }
            }

            return foundMessage;
        }

        private ValueTask<IPostgresMessage> ReadNextMessage(
            bool async, CancellationToken cancellationToken)
        {
            // Attempt a read without allocating a Task or combined
            // CancellationTokenSource.
            var messageFound = PostgresMessageRead(out var message);

            if (messageFound)
            {
                return new ValueTask<IPostgresMessage>(message);
            }

            var readTask = ReadNextMessageCore(async, cancellationToken);

            return new ValueTask<IPostgresMessage>(readTask);
        }

        private async Task<IPostgresMessage> ReadNextMessageCore(
            bool async, CancellationToken cancellationToken)
        {
            using (var kancel = _cancel.Combine(cancellationToken))
            while (true)
            {
                var messageFound = PostgresMessageRead(out var message);

                if (messageFound)
                {
                    return message;
                }

                CheckDisposed();

                _bufferOffset = 0;
                var readTask = Receive(
                    async, _buffer, 0, PostgresDbConnection.BufferSize,
                    kancel.Token);

                _bufferCount = await readTask.ConfigureAwait(false);

                if (_bufferCount == 0)
                {
                    throw new EndOfStreamException(
                        "PostgresConnection has ended.");
                }
            }
        }

        protected override DbCommand CreateDbCommand()
        {
            return new PostgresCommand("", this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckDisposed()
        {
            if (IsDisposed())
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsDisposed()
        {
            return Volatile.Read(ref _isDisposed) == 1;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (Interlocked.Exchange(ref _isDisposed, 1) == 1)
                {
                    return;
                }

                PostgresDbConnection.FreeBuffer(ref _buffer);
                MemoryStreamPool.Free(ref _writeBuffer);
                _connectionStream.Dispose(_stream);
                _cancel.TryCancelDispose();
            }

            base.Dispose(disposing);
        }
    }
}