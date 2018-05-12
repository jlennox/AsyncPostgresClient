using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Lennox.AsyncPostgresClient.BufferAccess;
using Lennox.AsyncPostgresClient.Exceptions;
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

                var result = reader.GetValue(0);

                await reader.ReadToEnd(async, cancellationToken)
                    .ConfigureAwait(false);

                return result;
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

        internal async ValueTask<CommandCompleteMessage> ExecuteSimpleCommand(
            bool async,
            CancellationToken cancellationToken)
        {
            await _connection.SimpleQuery(async, _command, cancellationToken)
                .ConfigureAwait(false);

            var lastCompleteMessage = default(CommandCompleteMessage);

            while (true)
            {
                var message = await _connection.
                    ReadNextMessage(async, cancellationToken)
                    .ConfigureAwait(false);

                switch (message)
                {
                    case CommandCompleteMessage completeMessage:
                        // If multiple commands were separated by semi-colons
                        // then multiple CommandCompleteMessage will arrive.
                        lastCompleteMessage = completeMessage;
                        break;
                    case ReadyForQueryMessage _:
                        return lastCompleteMessage;
                    default:
                        throw new PostgresInvalidMessageException(message);
                }
            }
        }

        public override Task<int> ExecuteNonQueryAsync(
            CancellationToken cancellationToken)
        {
            return ExecuteNonQuery(true, cancellationToken);
        }

        public override int ExecuteNonQuery()
        {
            _connection.CheckAsyncOnly();
            return ExecuteNonQuery(false, CancellationToken.None)
                .CompletedTaskValue();
        }

        private async Task<int> ExecuteNonQuery(
            bool async, CancellationToken cancellationToken)
        {
            var completeMessage = await ExecuteSimpleCommand(
                    async, cancellationToken)
                .ConfigureAwait(false);

            return ParseNumericValueFromNonQueryResponse(
                completeMessage.Tag) ?? 0;
        }

        internal static unsafe int? ParseNumericValueFromNonQueryResponse(
            string s)
        {
            // Example: INSERT 0 5
            //                   ^- this guy

            if (string.IsNullOrEmpty(s))
            {
                return null;
            }

            var lastSpace = s.LastIndexOf(' ');

            if (lastSpace == -1 || lastSpace == s.Length - 1)
            {
                return null;
            }

            var length = s.Length - lastSpace - 1;

            // int.MaxValue.ToString().Length
            const int intMaxValueLength = 10;

            if (length > intMaxValueLength)
            {
                return null;
            }

            var buffer = stackalloc byte[length];

            for (var i = 0; i < length; ++i)
            {
                var chr = s[lastSpace + 1 + i];

                if (chr < '0' || chr > '9')
                {
                    return null;
                }

                buffer[i] = (byte)chr;
            }

            return NumericBuffer.TryAsciiToInt(buffer, length, out var num)
                ? num
                : (int?)null;
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