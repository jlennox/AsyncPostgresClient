using System;
using System.Linq;
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

                CollectionAssert.AreEqual(new[] { 1 }, one.ToArray());
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
            using (var command = new PostgresCommand(
                "SELECT 0, 1, 2, true, false, 4.6 as foobar, null", connection))
            {
                var reader = await command.ExecuteReaderAsync(cancel);

                Assert.IsTrue(await reader.ReadAsync(cancel));
                Assert.AreEqual(7, reader.FieldCount);
                Assert.AreEqual("0", reader.GetString(0));
                Assert.AreEqual("1", reader.GetString(1));
                Assert.AreEqual("2", reader.GetString(2));
                Assert.AreEqual("t", reader.GetString(3));
                Assert.AreEqual("f", reader.GetString(4));
                Assert.AreEqual("4.6", reader.GetString(5));

                Assert.AreEqual(0, reader.GetInt16(0));
                Assert.AreEqual(0, reader.GetInt32(0));
                Assert.AreEqual(0, reader.GetInt64(0));
                Assert.AreEqual(0, reader.GetValue(0));

                Assert.AreEqual(1, reader.GetInt16(1));
                Assert.AreEqual(1, reader.GetInt32(1));
                Assert.AreEqual(1, reader.GetInt64(1));
                Assert.AreEqual(1, reader.GetValue(1));

                Assert.AreEqual(2, reader.GetInt16(2));
                Assert.AreEqual(2, reader.GetInt32(2));
                Assert.AreEqual(2, reader.GetValue(2));
                Assert.AreEqual(2, reader.GetInt64(2));

                Assert.AreEqual(true, reader.GetBoolean(3));
                Assert.AreEqual(true, reader.GetValue(3));
                Assert.AreEqual(false, reader.GetBoolean(4));
                Assert.AreEqual(false, reader.GetValue(4));

                Assert.AreEqual(4.6m, reader.GetDecimal(5));
                Assert.AreEqual(4.6m, reader.GetValue(5));
                Assert.AreEqual(4.6m, reader["foobar"]);

                Assert.AreEqual(DBNull.Value, reader.GetValue(6));

                Assert.ThrowsException<IndexOutOfRangeException>(
                    () => reader[999]);

                Assert.ThrowsException<IndexOutOfRangeException>(
                    () => reader["does not exist"]);

                Assert.IsFalse(await reader.ReadAsync(cancel));
            }
        }
    }
}
