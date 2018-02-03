using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Lennox.AsyncPostgresClient.Exceptions;
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

    public class PostgresDbConnection<TStream> : PostgresDbConnectionBase
    {
        private readonly IConnectionStream<TStream> _connectionStream;
        private TStream _stream;

        public PostgresDbConnection(
            string connectionString,
            IConnectionStream<TStream> connectionStream,
            bool asyncOnly)
            : base(connectionString, asyncOnly)
        {
            _connectionStream = connectionStream;
        }

        public PostgresDbConnection(
            string connectionString,
            IConnectionStream<TStream> connectionStream)
            : this(connectionString, connectionStream, false)
        {
        }

        protected override Task CreateConnection(
            CancellationToken cancellationToken)
        {
            _stream = _connectionStream.CreateTcpStream(
                PostgresConnectionString.Hostname,
                PostgresConnectionString.Port);

            return Task.CompletedTask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override Task Send(
            bool async, byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            return _connectionStream.Send(
                async, _stream, buffer, offset, count,
                cancellationToken);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override ValueTask<int> Receive(
            bool async, byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            return _connectionStream.Receive(
                async, _stream, buffer, offset, count,
                cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _connectionStream.Dispose(_stream);
            }

            base.Dispose(disposing);
        }
    }

    public abstract class PostgresDbConnectionBase : DbConnection
    {
        public override string ConnectionString { get; set; }
        public override string Database { get; }
        public override ConnectionState State { get; }
        public override string DataSource { get; }
        public override string ServerVersion { get; }

        public bool AsyncOnly { get; }

        public delegate void NoticeResponseEvent(
            PostgresDbConnectionBase connection,
            FieldValueResponse[] notices);

        public event NoticeResponseEvent NoticeResponse;

        protected readonly PostgresConnectionString PostgresConnectionString;
        private PostgresReadState _readState = new PostgresReadState();
        private PostgresClientState _clientState =
            PostgresClientState.CreateDefault();

        internal Encoding ClientEncoding => _clientState.ClientEncoding;
        internal Encoding ServerEncoding => _clientState.ServerEncoding;

        private MemoryStream _writeBuffer;
        private byte[] _buffer;
        private int _bufferOffset;
        private int _bufferCount;

        private int _isDisposed;
        private readonly object _disposeSync = new object();
        private readonly CancellationTokenSource _cancel =
            new CancellationTokenSource();

        protected PostgresDbConnectionBase(
            string connectionString,
            bool asyncOnly)
        {
            PostgresConnectionString = new PostgresConnectionString(connectionString);
            _buffer = PostgresDbConnection.GetBuffer();
            _writeBuffer = MemoryStreamPool.Get();
            AsyncOnly = asyncOnly;
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
            CheckAsyncOnly();

            OpenAsync(false, CancellationToken.None).Forget();
        }

        public override Task OpenAsync(
            CancellationToken cancellationToken)
        {
            return OpenAsync(true, cancellationToken);
        }

        protected abstract Task CreateConnection(
            CancellationToken cancellationToken);

        protected abstract Task Send(
            bool async, byte[] buffer, int offset, int count,
            CancellationToken cancellationToken);

        protected abstract ValueTask<int> Receive(
            bool async, byte[] buffer, int offset, int count,
            CancellationToken cancellationToken);

        private async Task OpenAsync(bool async,
            CancellationToken cancellationToken)
        {
            await CreateConnection(cancellationToken)
                .ConfigureAwait(false);

            const int messageCount = 3;
            var messages = ArrayPool<KeyValuePair<string, string>>
                .GetArray(messageCount);

            try
            {
                messages[0] = new KeyValuePair<string, string>(
                    "user", PostgresConnectionString.Username);
                messages[1] = new KeyValuePair<string, string>(
                    "client_encoding", PostgresConnectionString.Encoding);
                messages[2] = new KeyValuePair<string, string>(
                    "database", PostgresConnectionString.Database);

                WriteMessage(new StartupMessage {
                    MessageCount = messageCount,
                    Messages = messages
                });
            }
            finally
            {
                ArrayPool.Free(ref messages);
            }

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
                authOkMessage.AsssertIsOk();
            }

            var foundIdleMessage = false;

            do
            {
                var message = await ReadNextMessage(async, cancellationToken)
                    .ConfigureAwait(false);

                switch (message)
                {
                    case BackendKeyDataMessage keyDataMessage:
                        break;
                    case ReadyForQueryMessage readyMessage:
                        readyMessage.AssertType(TransactionIndicatorType.Idle);
                        foundIdleMessage = true;
                        break;
                    default:
                        throw new PostgresInvalidMessageException(message);
                }
            } while (!foundIdleMessage);
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
        internal void CheckAsyncOnly()
        {
            if (AsyncOnly)
            {
                throw new IOException("Non-async access has been disabled.");
            }
        }

        internal async Task Query(
            bool async, string query, CancellationToken cancellationToken)
        {
            WriteMessage(new QueryMessage {
                Query = query
            });

            await FlushWrites(async, cancellationToken).ConfigureAwait(false);
        }

        private void Authenticate(AuthenticationMessage authenticationMessage)
        {
            PasswordMessage passwordMessage;
            switch (authenticationMessage.AuthenticationMessageType)
            {
                case AuthenticationMessageType.MD5Password:
                    passwordMessage = PasswordMessage.CreateMd5(
                        authenticationMessage, _clientState,
                        PostgresConnectionString);
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
            while (true)
            {
                var foundMessage = PostgresMessage.ReadMessage(
                    _buffer, ref _bufferOffset, ref _bufferCount,
                    ref _readState, ref _clientState, out message);

                if (foundMessage)
                {
                    // https://www.postgresql.org/docs/10/static/protocol-flow.html#PROTOCOL-ASYNC
                    // TODO: Handle 'LISTEN'
                    switch (message)
                    {
                        case ErrorResponseMessage errorMessage:
                            throw new PostgresErrorException(errorMessage);
                        case NoticeResponseMessage noticeMessage:
                            NoticeResponse?.Invoke(
                                this, noticeMessage.PublicCloneNotices());
                            continue;
                        case ParameterStatusMessage paramMessage:
                            continue;
                    }
                }

                return foundMessage;
            }
        }

        internal ValueTask<IPostgresMessage> ReadNextMessage(
            bool async, CancellationToken cancellationToken)
        {
            // Attempt a read without allocating a Task or combined
            // CancellationTokenSource.
            var messageFound = PostgresMessageRead(out var message);

            if (messageFound)
            {
                return new ValueTask<IPostgresMessage>(message);
            }

            return ReadNextMessageCore(async, cancellationToken);
        }

        private async ValueTask<IPostgresMessage> ReadNextMessageCore(
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
                _cancel.TryCancelDispose();
            }

            base.Dispose(disposing);
        }
    }
}