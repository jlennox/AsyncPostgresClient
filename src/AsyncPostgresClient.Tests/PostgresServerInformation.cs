using System;
using System.Threading;
using System.Threading.Tasks;

namespace Lennox.AsyncPostgresClient.Tests
{
    public static class PostgresServerInformation
    {
        public static string ConnectionString =>
            Environment.GetEnvironmentVariable("PSQL_TEST_SERVER");

        // Static. Used for testing non-live requests.
        public static string FakeConnectionString =>
            "Server=server;User ID=Test_User;Password=56ffc8c3cd680bf96ba600943f149b92;Database=test_db";

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