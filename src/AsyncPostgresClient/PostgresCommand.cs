using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Lennox.AsyncPostgresClient.Extension;
using Lennox.AsyncPostgresClient.PostgresTypes;

namespace Lennox.AsyncPostgresClient
{
    public class PostgresCommand : DbCommand
    {
        // Backed by a field to avoid "Virtual member call in constructor."
        public override string CommandText
        {
            get => _command;
            set => _command = value;
        }

        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection DbConnection { get; set; }
        protected override DbParameterCollection DbParameterCollection { get; }
        protected override DbTransaction DbTransaction { get; set; }
        public override bool DesignTimeVisible { get; set; }

        internal bool DoNotLoadTypeCollection { get; set; }
        internal PostgresTypeCollection TypeCollection { get; private set; }

        private string _command;
        private readonly PostgresDbConnectionBase _connection;

        public PostgresCommand(
            string command,
            PostgresDbConnectionBase connection)
        {
            _command = command;
            _connection = connection;
        }

        public override void Cancel()
        {
            throw new NotImplementedException();
        }

        public override int ExecuteNonQuery()
        {
            _connection.CheckAsyncOnly();
            throw new NotImplementedException();
        }

        public override void Prepare()
        {
            throw new NotImplementedException();
        }

        protected override DbParameter CreateDbParameter()
        {
            throw new NotImplementedException();
        }

        public override object ExecuteScalar()
        {
            _connection.CheckAsyncOnly();

            var scalarTask = ExecuteScalar(true, CancellationToken.None);

            return scalarTask.CompletedTaskValue();
        }

        public override Task<object> ExecuteScalarAsync(
            CancellationToken cancellationToken)
        {
            return ExecuteScalar(true, cancellationToken).AsTask();
        }

        private async ValueTask<object> ExecuteScalar(
            bool async,
            CancellationToken cancellationToken)
        {
            await _connection.Query(async, CommandText, cancellationToken)
                .ConfigureAwait(false);

            return 0;
        }

        protected override DbDataReader ExecuteDbDataReader(
            CommandBehavior behavior)
        {
            _connection.CheckAsyncOnly();

            var readerTask = ExecuteDbDataReader(
                false, behavior, CancellationToken.None);

            return readerTask.CompletedTaskValue();
        }

        protected override Task<DbDataReader> ExecuteDbDataReaderAsync(
            CommandBehavior behavior,
            CancellationToken cancellationToken)
        {
            return ExecuteDbDataReader(
                false, behavior, CancellationToken.None).AsTask();
        }

        internal async ValueTask<DbDataReader> ExecuteDbDataReader(
            bool async,
            CommandBehavior behavior,
            CancellationToken cancellationToken)
        {
            if (!DoNotLoadTypeCollection)
            {
                TypeCollection = await _connection.GetTypeCollection(
                    async, cancellationToken).ConfigureAwait(false);
            }

            await _connection.Query(async, CommandText, cancellationToken)
                .ConfigureAwait(false);

            var reader = new PostgresDbDataReader(
                behavior, _connection, this, cancellationToken);

            return reader;
        }

        public override Task<int> ExecuteNonQueryAsync(
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }
}