﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
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
        protected override DbParameterCollection DbParameterCollection => PostgresParameters;
        protected override DbTransaction DbTransaction { get; set; }
        public override bool DesignTimeVisible { get; set; }

        internal readonly PostgresDbParameterCollection PostgresParameters =
             new PostgresDbParameterCollection();

        internal bool DoNotLoadTypeCollection { get; set; }
        internal PostgresTypeCollection TypeCollection { get; private set; }

        private string _command;
        private readonly PostgresDbConnectionBase _connection;

        public PostgresCommand(
            string command,
            PostgresDbConnectionBase connection)
        {
            Argument.HasValue(nameof(connection), connection);

            _command = command;
            _connection = connection;
        }

        internal PostgresCommand()
        {
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
            return new PostgresParameter();
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
            using (var reader = await ExecuteDbDataReader(
                    async, CommandBehavior.Default, cancellationToken)
                .ConfigureAwait(false) as PostgresDbDataReader)
            {
                var hasResults = await reader.Read(async, cancellationToken)
                    .ConfigureAwait(false);

                if (!hasResults || reader.FieldCount == 0)
                {
                    return null;
                }

                return reader.GetValue(0);
            }
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ValueTask<DbDataReader> ExecuteDbDataReader(
            bool async,
            CancellationToken cancellationToken)
        {
            return ExecuteDbDataReader(
                async, CommandBehavior.Default, cancellationToken);
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

            await _connection.Query(async, this, cancellationToken)
                .ConfigureAwait(false);

            var reader = new PostgresDbDataReader(
                behavior, _connection, this, cancellationToken);

            try
            {
                await reader.ReadUntilData(async, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                reader.TryDispose();
                throw;
            }

            return reader;
        }

        public override Task<int> ExecuteNonQueryAsync(
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        internal async ValueTask<bool> ExecuteUntilFinished(
            bool async, CancellationToken cancellationToken)
        {
            var reader = await ExecuteDbDataReader(
                    async, CommandBehavior.Default, cancellationToken)
                .ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken)
                .ConfigureAwait(false))
            { }

            return true;
        }

        internal IReadOnlyList<string> GetRewrittenCommandText()
        {
            // TODO: First argument should not be null.
            return PostgresSqlCommandParser.Perform(null, this);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }
}