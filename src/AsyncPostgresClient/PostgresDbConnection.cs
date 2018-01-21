using AsyncPostgresClient.Extension;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncPostgresClient
{
    public class PostgresDbConnection : DbConnection
    {
        public override string ConnectionString { get; set; }
        public override string Database { get; }
        public override ConnectionState State { get; }
        public override string DataSource { get; }
        public override string ServerVersion { get; }

        private readonly IConnectionStream _connectionStream;
        private readonly PostgresConnectionString _connectionString;
        //private PostgresReadState _readState = new PostgresReadState();
        private PostgresClientState _clientState =
            PostgresClientState.CreateDefault();

        private Stream _stream;

        public PostgresDbConnection(
            string connectionString,
            IConnectionStream connectionStream)
        {
            _connectionString = new PostgresConnectionString(connectionString);
            _connectionStream = connectionStream;
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
            var ms = MemoryStreamPool.Get();
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

                StartupMessage.WriteMessage(ref _clientState, ms,
                    messageCount, messages);

                await ms.CopyToAsync(async, _stream, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                MemoryStreamPool.Free(ref ms);
                ArrayPool.Free(ref messages);
            }
        }

        protected override DbCommand CreateDbCommand()
        {
            return new PostgresDbCommand();
        }

        public new void Dispose()
        {
            _stream.Dispose();
        }
    }
}
