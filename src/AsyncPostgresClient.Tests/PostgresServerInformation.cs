using System;
using System.Threading;
using System.Threading.Tasks;

namespace Lennox.AsyncPostgresClient.Tests
{
    public static class PostgresServerInformation
    {
        public static string ConnectionString =>
            Environment.GetEnvironmentVariable("PSQL_TEST_SERVER");


        public static Task<PostgresDbConnection> Open()
        {
            return Open(CancellationToken.None);
        }

        public static async Task<PostgresDbConnection> Open(
            CancellationToken cancellationToken)
        {
            var connection = new PostgresDbConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            return connection;
        }
    }
}