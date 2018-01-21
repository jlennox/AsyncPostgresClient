using AsyncPostgresClient.Extension;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncPostgresClient
{
    public class PostgresDbConnection : DbConnection
    {
        private const int _bufferSize = 16 * 1024;

        // Don't use the global array pool. This pool will contain all large
        // buffers of the same size.
        private static readonly IArrayPool<byte> _bufferPool =
            ArrayPool<byte>.InstanceDefault();

        public override string ConnectionString { get; set; }
        public override string Database { get; }
        public override ConnectionState State { get; }
        public override string DataSource { get; }
        public override string ServerVersion { get; }

        private readonly IConnectionStream _connectionStream;
        private readonly PostgresConnectionString _connectionString;
        private PostgresReadState _readState = new PostgresReadState();
        private PostgresClientState _clientState =
            PostgresClientState.CreateDefault();

        private Stream _stream;
        private MemoryStream _writeBuffer;
        private byte[] _buffer;
        private int _bufferOffset;
        private int _bufferCount;
        private IPostgresMessage _bufferedMessage;

        private int _isDisposed;
        private readonly object _disposeSync = new object();
        private readonly CancellationTokenSource _cancel =
            new CancellationTokenSource();

        public PostgresDbConnection(
            string connectionString,
            IConnectionStream connectionStream)
        {
            _connectionString = new PostgresConnectionString(connectionString);
            _connectionStream = connectionStream;
            _buffer = _bufferPool.Get(_bufferSize);
            _writeBuffer = MemoryStreamPool.Get();
        }

        public PostgresDbConnection(string connectionString)
            : this(connectionString, ClrConnectionStream.Default)
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
                await _writeBuffer.CopyToAsync(async, _stream, kancel.Token)
                    .ConfigureAwait(false);
            }

            _writeBuffer.Position = 0;
            _writeBuffer.SetLength(0);
        }

        internal async Task Query(
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

        // TODO: This should be generic with a type constraint.
        private bool HandleSystemMessage(IPostgresMessage message)
        {
            if (message is ErrorResponseMessage errorMessage)
            {
                using (errorMessage)
                {
                    throw new PostgresErrorException(errorMessage);
                }
            }

            if (message is AuthenticationMessage authMessage)
            {
                using (authMessage)
                {
                    Authenticate(authMessage);
                    return true;
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool PostgresMessageRead(out IPostgresMessage message)
        {
            if (_bufferedMessage != null)
            {
                message = _bufferedMessage;
                _bufferedMessage = null;
                return true;
            }

            while (true)
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
                        case AuthenticationMessage authMessage:
                            Authenticate(authMessage);
                            continue;
                    }
                }

                return foundMessage;
            }
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
                var readTask = _stream.ReadAsync(
                    async, _buffer, 0, _bufferSize, kancel.Token);

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

                _bufferPool.Free(ref _buffer);
                MemoryStreamPool.Free(ref _writeBuffer);
                _stream.TryDispose();
                _cancel.TryCancelDispose();
            }

            base.Dispose(disposing);
        }
    }
}