using System;
using System.Collections;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Lennox.AsyncPostgresClient.Exceptions;
using Lennox.AsyncPostgresClient.Extension;

namespace Lennox.AsyncPostgresClient
{
    public class PostgresDbDataReader : DbDataReader
    {
        public override int FieldCount
        {
            get
            {
                if (_fieldCount == -1)
                {
                    throw new NotSupportedException(
                        "Results have not been read.");
                }

                return _fieldCount;
            }
        }

        public override object this[int ordinal]
        {
            get { throw new NotImplementedException(); }
        }

        public override object this[string name]
        {
            get { throw new NotImplementedException(); }
        }

        public override int RecordsAffected { get; }
        public override bool HasRows { get; }
        public override bool IsClosed { get; }
        public override int Depth { get; }

        private readonly CommandBehavior _behavior;
        private readonly PostgresDbConnectionBase _connection;
        private readonly CancellationToken _cancellationToken;

        private int _fieldCount = -1;
        private DataRowMessage? _lastDataRowMessage;

        public PostgresDbDataReader(
            CommandBehavior behavior,
            PostgresDbConnectionBase connection,
            CancellationToken cancellationToken)
        {
            _behavior = behavior;
            _connection = connection;
            _cancellationToken = cancellationToken;
        }

        public override bool GetBoolean(int ordinal)
        {
            return false;
        }

        public override byte GetByte(int ordinal)
        {
            throw new NotImplementedException();
        }

        public override long GetBytes(
            int ordinal, long dataOffset, byte[] buffer,
            int bufferOffset, int length)
        {
            throw new NotImplementedException();
        }

        public override char GetChar(int ordinal)
        {
            throw new NotImplementedException();
        }

        public override long GetChars(
            int ordinal, long dataOffset, char[] buffer,
            int bufferOffset, int length)
        {
            throw new NotImplementedException();
        }

        public override string GetDataTypeName(int ordinal)
        {
            throw new NotImplementedException();
        }

        public override DateTime GetDateTime(int ordinal)
        {
            throw new NotImplementedException();
        }

        public override decimal GetDecimal(int ordinal)
        {
            throw new NotImplementedException();
        }

        public override double GetDouble(int ordinal)
        {
            throw new NotImplementedException();
        }

        public override Type GetFieldType(int ordinal)
        {
            throw new NotImplementedException();
        }

        public override float GetFloat(int ordinal)
        {
            throw new NotImplementedException();
        }

        public override Guid GetGuid(int ordinal)
        {
            throw new NotImplementedException();
        }

        public override short GetInt16(int ordinal)
        {
            throw new NotImplementedException();
        }

        public override int GetInt32(int ordinal)
        {
            throw new NotImplementedException();
        }

        public override long GetInt64(int ordinal)
        {
            throw new NotImplementedException();
        }

        public override string GetName(int ordinal)
        {
            throw new NotImplementedException();
        }

        public override int GetOrdinal(string name)
        {
            throw new NotImplementedException();
        }

        public override string GetString(int ordinal)
        {
            throw new NotImplementedException();
        }

        public override object GetValue(int ordinal)
        {
            throw new NotImplementedException();
        }

        public override int GetValues(object[] values)
        {
            throw new NotImplementedException();
        }

        public override bool IsDBNull(int ordinal)
        {
            _connection.CheckAsyncOnly();
            throw new NotImplementedException();
        }

        public override Task<bool> IsDBNullAsync(
            int ordinal, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override bool NextResult()
        {
            _connection.CheckAsyncOnly();
            throw new NotImplementedException();
        }

        public override Task<bool> NextResultAsync(
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override bool Read()
        {
            _connection.CheckAsyncOnly();
            var readTask = Read(false, CancellationToken.None);
            return readTask.CompletedTaskValue();
        }

        public override Task<bool> ReadAsync(
            CancellationToken cancellationToken)
        {
            return Read(true, cancellationToken).AsTask();
        }

        private bool _commandCompleted;

        private async ValueTask<bool> Read(
            bool async, CancellationToken cancellationToken)
        {
            // https://www.postgresql.org/docs/10/static/protocol-flow.html#idm46428663987712
            while (true)
            {
                var message = await _connection
                    .ReadNextMessage(async, cancellationToken)
                    .ConfigureAwait(false);

                switch (message)
                {
                    case CommandCompleteMessage completedMessage:
                        _commandCompleted = true;
                        continue;
                    case CopyInResponseMessage copyInMessage:
                        break;
                    case CopyOutResponseMessage copyOutMessage:
                        break;
                    case RowDescriptionMessage descriptionMessage:
                        _fieldCount = descriptionMessage.FieldCount;
                        break;
                    case DataRowMessage dataRowMessage:
                        _lastDataRowMessage?.TryDispose();

                        _lastDataRowMessage = dataRowMessage;
                        return true;
                    case EmptyQueryResponseMessage emptyMessage:
                        // "If a completely empty (no contents other than
                        // whitespace) query string is received, the response
                        // is EmptyQueryResponse followed by ReadyForQuery."
                        _commandCompleted = true;
                        continue;
                    case ReadyForQueryMessage readyMessage:
                        if (!_commandCompleted)
                        {
                            throw new PostgresInvalidMessageException(message,
                                "Ready message must not appear before command complete message.");
                        }

                        readyMessage.AssertType(TransactionIndicatorType.Idle);
                        return false;
                    default:
                        throw new PostgresInvalidMessageException(message);
                }
            }
        }

        public override Task<T> GetFieldValueAsync<T>(
            int ordinal, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override IEnumerator GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public override void Close()
        {
            _lastDataRowMessage?.TryDispose();
            base.Close();
        }
    }
}