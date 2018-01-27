using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Lennox.AsyncPostgresClient
{
    public class PostgresCommand : DbCommand
    {
        public override string CommandText { get; set; }
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection DbConnection { get; set; }
        protected override DbParameterCollection DbParameterCollection { get; }
        protected override DbTransaction DbTransaction { get; set; }
        public override bool DesignTimeVisible { get; set; }

        private readonly string _command;
        private readonly IPosgresDbConnection _connection;

        public PostgresCommand(string command, IPosgresDbConnection connection)
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
            throw new NotImplementedException();
        }

        public override object ExecuteScalar()
        {
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

        public override async Task<object> ExecuteScalarAsync(
            CancellationToken cancellationToken)
        {
            await _connection.Query(true, _command, cancellationToken);
            return 0;
        }

        protected override DbDataReader ExecuteDbDataReader(
            CommandBehavior behavior)
        {
            throw new NotImplementedException();
        }

        protected override Task<DbDataReader> ExecuteDbDataReaderAsync(
            CommandBehavior behavior,
            CancellationToken cancellationToken)
        {
            var reader = new PostgresDbDataReader(behavior, cancellationToken);

            return Task.FromResult<DbDataReader>(reader);
        }

        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }
}