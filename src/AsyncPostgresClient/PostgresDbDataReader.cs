using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Lennox.AsyncPostgresClient.Exceptions;
using Lennox.AsyncPostgresClient.Extension;
using Lennox.AsyncPostgresClient.PostgresTypes;

namespace Lennox.AsyncPostgresClient
{
    // Reference source:
    // https://github.com/dotnet/corefx/blob/master/src/System.Data.SqlClient/src/System/Data/SqlClient/SqlDataReader.cs
    // https://msdn.microsoft.com/en-us/library/system.data.common.dbdatareader(v=vs.110).aspx
    public class PostgresDbDataReader : DbDataReader
    {
        public override object this[int ordinal] => GetValue(ordinal);
        public override object this[string name]
        {
            get
            {
                var ordinal = ColumnByName(name, out var _);

                return GetValue(ordinal);
            }
        }

        public override int FieldCount
        {
            get
            {
                CheckIsClosed();

                return _fieldCount;
            }
        }

        public override bool HasRows
        {
            get
            {
                CheckIsClosed();

                return _hasRows;
            }
        }

        public override int Depth
        {
            get
            {
                CheckIsClosed();

                return _depth;
            }
        }

        public override bool IsClosed => _isClosed;
        public override int RecordsAffected => _recordsAffected;

        private int _recordsAffected = 0;
        private bool _hasRows;
        private bool _isClosed = false;
        private int _depth = 0;

        private readonly PostgresDbConnectionBase _connection;
        private readonly PostgresCommand _command;
        private readonly CancellationToken _cancellationToken;

        #region CommandBehavior
        // https://docs.microsoft.com/en-us/dotnet/api/system.data.commandbehavior?view=netframework-4.7.1
        private readonly CommandBehavior _behavior;

        /// <summary>When the command is executed, the associated Connection
        /// object is closed when the associated DataReader object is closed.
        /// </summary>
        private readonly bool _behaviorCloseConnection;
        /// <summary>The query returns column and primary key information.
        /// </summary>
        private readonly bool _behaviorKeyInfo;
        /// <summary>The query returns column information only. When using
        /// SchemaOnly, the .NET Framework Data Provider for SQL Server
        /// precedes the statement being executed with SET FMTONLY ON.
        /// </summary>
        private readonly bool _behaviorSchemaOnly;
        /// <summary>Provides a way for the DataReader to handle rows that
        /// contain columns with large binary values.Rather than loading the
        /// entire row, SequentialAccess enables the DataReader to load data as
        /// a stream. You can then use the GetBytes or GetChars method to
        /// specify a byte location to start the read operation, and a limited
        /// buffer size for the data being returned.</summary>
        private readonly bool _behaviorSequentialAccess;
        /// <summary>The query returns a single result set.</summary>
        private readonly bool _behaviorSinglaResult;
        /// <summary>The query is expected to return a single row of the first
        /// result set. Execution of the query may affect the database state.
        /// Some .NET Framework data providers may, but are not required to,
        /// use this information to optimize the performance of the command.
        /// When you specify SingleRow with the ExecuteReader() method of the
        /// OleDbCommand object, the .NET Framework Data Provider for OLE DB
        /// performs binding using the OLE DB IRow interface if it is
        /// available. Otherwise, it uses the IRowset interface. If your SQL
        /// statement is expected to return only a single row, specifying
        /// SingleRow can also improve application performance. It is possible
        /// to specify SingleRow when executing queries that are expected to
        /// return multiple result sets. In that case, where both a
        /// multi-result set SQL query and single row are specified, the result
        /// returned will contain only the first row of the first result set.
        /// The other result sets of the query will not be returned.</summary>
        private readonly bool _behaviorSingleRow;
        #endregion

        private int _fieldCount = 0;
        private RowDescriptionMessage? _descriptionMessage;
        private DataRowMessage? _lastDataRowMessage;
        private bool _commandCompleted;

        public PostgresDbDataReader(
            CommandBehavior behavior,
            PostgresDbConnectionBase connection,
            PostgresCommand command,
            CancellationToken cancellationToken)
        {
            _behavior = behavior;
            _connection = connection;
            _command = command;
            _cancellationToken = cancellationToken;

            _behaviorCloseConnection = behavior
                .HasFlag(CommandBehavior.CloseConnection);
            _behaviorKeyInfo = behavior
                .HasFlag(CommandBehavior.KeyInfo);
            _behaviorSchemaOnly = behavior
                .HasFlag(CommandBehavior.SchemaOnly);
            _behaviorSequentialAccess = behavior
                .HasFlag(CommandBehavior.SequentialAccess);
            _behaviorSinglaResult = behavior
                .HasFlag(CommandBehavior.SingleResult);
            _behaviorSingleRow = behavior
                .HasFlag(CommandBehavior.SingleRow);
        }

        public override bool GetBoolean(int ordinal)
        {
            var row = GetDataRow(ordinal);
            return PostgresTypeConverter
                .ForBool(row, _connection.ClientState);
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
            var row = GetDataRow(ordinal);
            return PostgresTypeConverter
                .ForDecimal(row, _connection.ClientState);
        }

        public override double GetDouble(int ordinal)
        {
            var row = GetDataRow(ordinal);
            return (double)PostgresTypeConverter
                .ForDecimal(row, _connection.ClientState);
        }

        public override Type GetFieldType(int ordinal)
        {
            throw new NotImplementedException();
        }

        public override float GetFloat(int ordinal)
        {
            var row = GetDataRow(ordinal);
            return PostgresTypeConverter
                .ForFloat8(row, _connection.ClientState);
        }

        public override Guid GetGuid(int ordinal)
        {
            var row = GetDataRow(ordinal);
            return PostgresTypeConverter
                .ForUuid(row, _connection.ClientState);
        }

        public override short GetInt16(int ordinal)
        {
            var row = GetDataRow(ordinal);
            return PostgresTypeConverter
                .ForInt2(row, _connection.ClientState);
        }

        public override int GetInt32(int ordinal)
        {
            var row = GetDataRow(ordinal);
            return PostgresTypeConverter
                .ForInt4(row, _connection.ClientState);
        }

        public override long GetInt64(int ordinal)
        {
            var row = GetDataRow(ordinal);
            return PostgresTypeConverter
                .ForInt8(row, _connection.ClientState);
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
            var row = GetDataRow(ordinal);
            return PostgresTypeConverter
                .ForString(row, _connection.ClientState);
        }

        public override object GetValue(int ordinal)
        {
            return RowDataObject(ordinal);
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
            return false;
        }

        public override Task<bool> NextResultAsync(
            CancellationToken cancellationToken)
        {
            return TaskCache.False;
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

        internal async ValueTask<bool> Read(
            bool async, CancellationToken cancellationToken)
        {
            CheckIsClosed();

            // https://www.postgresql.org/docs/10/static/protocol-flow.html#idm46428663987712
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var message = await _connection
                    .ReadNextMessage(async, cancellationToken)
                    .ConfigureAwait(false);

                switch (message)
                {
                    case CommandCompleteMessage completedMessage:
                        _commandCompleted = true;

                        if (_behaviorSinglaResult)
                        {
                            Close();
                            return false;
                        }

                        continue;
                    case CopyInResponseMessage copyInMessage:
                        break;
                    case CopyOutResponseMessage copyOutMessage:
                        break;
                    case RowDescriptionMessage descriptionMessage:
                        _descriptionMessage?.TryDispose();
                        _fieldCount = descriptionMessage.FieldCount;
                        _descriptionMessage = descriptionMessage;
                        break;
                    case DataRowMessage dataRowMessage:
                        _hasRows = true;

                        _lastDataRowMessage?.TryDispose();
                        _lastDataRowMessage = dataRowMessage;
                        return true;
                    case EmptyQueryResponseMessage emptyMessage:
                        // "If a completely empty (no contents other than
                        // whitespace) query string is received, the response
                        // is EmptyQueryResponse followed by ReadyForQuery."
                        _hasRows = false;
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
            _isClosed = true;
            _descriptionMessage?.TryDispose();
            _lastDataRowMessage?.TryDispose();
            base.Close();
        }

        private object RowDataObject(int ordinal)
        {
            var row = GetDataRow(ordinal);

            if (row.IsNull)
            {
                return DBNull.Value;
            }

            if (!_descriptionMessage.HasValue)
            {
                // TODO.
                throw new InvalidOperationException();
            }

            var field = _descriptionMessage.Value.Fields[ordinal];
            var oid = field.DataTypeObjectId;

            return _command.TypeCollection
                .Convert(oid, row, _connection.ClientState);
        }

        private DataRow GetDataRow(string name)
        {
            return GetDataRow(GetOrdinal(name));
        }

        private DataRow GetDataRow(int ordinal)
        {
            if (ordinal > _fieldCount)
            {
                throw new IndexOutOfRangeException(
                    $"The index passed was outside the range of 0 through {_fieldCount}.");
            }

            if (!_lastDataRowMessage.HasValue)
            {
                // TODO.
                throw new InvalidOperationException();
            }

            return GetDataRowUnchecked(ordinal);
        }

        private int ColumnByName(string name, out ColumnDescription description)
        {
            if (!_descriptionMessage.HasValue)
            {
                // TODO.
                throw new InvalidOperationException();
            }

            for (var i = 0; i < _fieldCount; ++i)
            {
                var field = _descriptionMessage.Value.Fields[i];

                if (field.Name == name)
                {
                    description = field;
                    return i;
                }
            }

            throw new IndexOutOfRangeException(
                "No column with the specified name was found.");
        }

        private ColumnDescription GetColumnDescription(int ordinal)
        {
            throw new Exception();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private DataRow GetDataRowUnchecked(int ordinal)
        {
            return _lastDataRowMessage.Value.Rows[ordinal];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckIsClosed()
        {
            if (IsClosed)
            {
                throw new InvalidOperationException();
            }
        }
    }
}