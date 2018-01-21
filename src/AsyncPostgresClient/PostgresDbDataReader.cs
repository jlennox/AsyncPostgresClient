using System;
using System.Collections;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncPostgresClient
{
    public class PostgresDbDataReader : DbDataReader
    {
        public override int FieldCount { get; }

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
        private readonly CancellationToken _cancellationToken;

        public PostgresDbDataReader(
            CommandBehavior behavior,
            CancellationToken cancellationToken)
        {
            _behavior = behavior;
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
            throw new NotImplementedException();
        }

        public override bool NextResult()
        {
            throw new NotImplementedException();
        }

        public override bool Read()
        {
            throw new NotImplementedException();
        }

        public override IEnumerator GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public override Task<T> GetFieldValueAsync<T>(
            int ordinal, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> IsDBNullAsync(
            int ordinal, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> NextResultAsync(
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> ReadAsync(
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}