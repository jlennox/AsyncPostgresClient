using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Threading;
using Lennox.AsyncPostgresClient.Extension;

namespace Lennox.AsyncPostgresClient
{
    public class PostgresTransaction : DbTransaction
    {
        protected override DbConnection DbConnection => _connection;
        public override IsolationLevel IsolationLevel { get; }

        private readonly PostgresDbConnectionBase _connection;

        internal PostgresTransaction(
            PostgresDbConnectionBase connection,
            IsolationLevel isolationLevel)
        {
            _connection = connection;
            IsolationLevel = isolationLevel;
        }

        public override void Commit()
        {
            SimpleQuery("COMMIT");
        }

        public override void Rollback()
        {
            SimpleQuery("ROLLBACK");
        }

        private void SimpleQuery(string s)
        {
            _connection.SimpleQuery(false, s, CancellationToken.None)
                .AssertCompleted();
        }
    }
}
