using System;

namespace AsyncPostgresClient.Tests
{
    public static class PostgresServerInformation
    {
        public static string ConnectionString =>
            Environment.GetEnvironmentVariable("PSQL_TEST_SERVER");

        public static PostgresDbConnection Open()
        {
            return new PostgresDbConnection(ConnectionString);
        }
    }
}