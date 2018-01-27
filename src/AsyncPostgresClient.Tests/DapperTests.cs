using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lennox.AsyncPostgresClient.Tests
{
    [TestClass]
    public class DapperTests
    {
        [TestMethod]
        public async Task TestDapperSelectOne()
        {
            using (var connection = await PostgresServerInformation.Open())
            {
                var one = await connection.QueryAsync<int>("SELECT 1");

                Assert.AreEqual(1, one);
            }
        }

        [TestMethod]
        public async Task TestExecuteScalarAsync()
        {
            var cancel = CancellationToken.None;

            using (var connection = await PostgresServerInformation.Open())
            using (var command = new PostgresCommand("SELECT 1", connection))
            {
                var one = await command.ExecuteScalarAsync(cancel);
                Assert.AreEqual(1, one);
            }
        }
    }
}
