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

        [DataTestMethod]
        [DataRow(false, DisplayName = "Text results")]
        [DataRow(true, DisplayName = "Binary results")]
        public async Task TestExecuteReaderAsync(bool useBinary)
        {
            var cancel = CancellationToken.None;

            using (var connection = await PostgresServerInformation.Open())
            using (var command = new PostgresCommand(
                "SELECT 0::int2, 1::int4, 2::int8, true, false, 4.6 as foobar, null", connection))
            {
                connection.QueryResultFormat = useBinary
                    ? PostgresFormatCode.Binary
                    : PostgresFormatCode.Text;

                var reader = await command.ExecuteReaderAsync(cancel);

                Assert.IsTrue(await reader.ReadAsync(cancel));
                Assert.AreEqual(7, reader.FieldCount);

                Assert.AreEqual(0, reader.GetInt16(0));
                Assert.AreEqual((short)0, reader.GetValue(0));

                Assert.AreEqual(1, reader.GetInt32(1));
                Assert.AreEqual(1, reader.GetValue(1));

                Assert.AreEqual(2, reader.GetInt64(2));
                Assert.AreEqual(2L, reader.GetValue(2));                

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

        [DataTestMethod]
        [DataRow(false, DisplayName = "Text results")]
        [DataRow(true, DisplayName = "Binary results")]
        public async Task BasicTestNumericTypes(bool useBinary)
        {
            var cancel = CancellationToken.None;

            using (var connection = await PostgresServerInformation.Open())
            using (var command = new PostgresCommand(
                "SELECT 500.5::numeric, 500.5::float4, 500.5::float8, 500.5::money", connection))
            {
                connection.QueryResultFormat = useBinary
                    ? PostgresFormatCode.Binary
                    : PostgresFormatCode.Text;

                var reader = await command.ExecuteReaderAsync(cancel);

                Assert.Inconclusive("TODO: Write this test");

                Assert.IsFalse(await reader.ReadAsync(cancel));
            }
        }

        [DataTestMethod]
        [DataRow(false, DisplayName = "Text results")]
        [DataRow(true, DisplayName = "Binary results")]
        public async Task BasicTestTimeTypes(bool useBinary)
        {
            var cancel = CancellationToken.None;

            using (var connection = await PostgresServerInformation.Open())
            using (var command = new PostgresCommand(
                "SELECT '2001-09-27 23:00:00'::timestamp", connection))
            {
                connection.QueryResultFormat = useBinary
                    ? PostgresFormatCode.Binary
                    : PostgresFormatCode.Text;

                var reader = await command.ExecuteReaderAsync(cancel);

                Assert.Inconclusive("TODO: Write this test");

                Assert.IsFalse(await reader.ReadAsync(cancel));
            }
        }
    }
}
