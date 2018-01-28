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

        [TestMethod]
        public async Task TestExecuteReaderAsync()
        {
            var cancel = CancellationToken.None;

            using (var connection = await PostgresServerInformation.Open())
            using (var command = new PostgresCommand("SELECT 1, 2, 3", connection))
            {
                var reader = await command.ExecuteReaderAsync(cancel);

                Assert.IsTrue(await reader.ReadAsync(cancel));
                Assert.AreEqual(3, reader.FieldCount);
                Assert.IsTrue(reader[0] is int);
                Assert.IsTrue(reader[1] is int);
                Assert.IsTrue(reader[2] is int);
                Assert.AreEqual(1, reader[0]);
                Assert.AreEqual(2, reader[1]);
                Assert.AreEqual(3, reader[2]);
                Assert.IsFalse(await reader.ReadAsync(cancel));
            }
        }
    }
}
