using System;
using System.Threading;
using System.Threading.Tasks;
using Lennox.AsyncPostgresClient.Diagnostic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lennox.AsyncPostgresClient.Tests
{
    [TestClass]
    public class TypeSerializationTests
    {
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
                Assert.IsTrue(await reader.ReadAsync(cancel));

                Assert.Inconclusive("TODO: Write this test");

                Assert.IsFalse(await reader.ReadAsync(cancel));
            }
        }

        [DataTestMethod]
        [DataRow(false, "float4", DisplayName = "Text float4 results")]
        [DataRow(true, "float4", DisplayName = "Binary float4 results")]
        [DataRow(false, "float8", DisplayName = "Text float8 results")]
        [DataRow(true, "float8", DisplayName = "Binary float8 results")]
        public async Task FloatingPointTest(bool useBinary, string floatFormat)
        {
            DebugLogger.Enabled = true;

            var cancel = CancellationToken.None;

            const decimal precision = 0.001m;

            using (var connection = await PostgresServerInformation.Open())
            {
                await connection.SendPropertyAsync(cancel,
                    new PostgresPropertySetting(
                        PostgresPropertyName.ExtraFloatDigits,
                        "3"));

                using (var command = new PostgresCommand(
                    $"SELECT 123456789.005::{floatFormat}, 500.0123456789::{floatFormat}", connection))
                {
                    connection.QueryResultFormat = useBinary
                        ? PostgresFormatCode.Binary
                        : PostgresFormatCode.Text;

                    var reader = await command.ExecuteReaderAsync(cancel);
                    Assert.IsTrue(await reader.ReadAsync(cancel));

                    switch (floatFormat)
                    {
                        case "float4":
                            var val1 = reader.GetFloat(0);
                            var val2 = reader.GetFloat(1);
                            NumericAsserts.FloatEquals(123456789.005f, val1, precision);
                            NumericAsserts.FloatEquals(500.012f, val2, precision);
                            break;
                        case "float8":
                            var valb1 = reader.GetDouble(0);
                            var valb2 = reader.GetDouble(1);
                            NumericAsserts.FloatEquals(123456789.005, valb1, precision);
                            NumericAsserts.FloatEquals(500.012f, valb2, precision);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(floatFormat);
                    }

                    Assert.IsFalse(await reader.ReadAsync(cancel));
                }
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
                "SELECT '2001-09-27 23:00:00'::timestamp, '2002-10-28'::date at time zone 'PST'", connection))
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
        public async Task BasicTestDateTypes(bool useBinary)
        {
            var cancel = CancellationToken.None;

            using (var connection = await PostgresServerInformation.Open())
            using (var command = new PostgresCommand(
                "SELECT '2001-09-27'::date", connection))
            {
                connection.QueryResultFormat = useBinary
                    ? PostgresFormatCode.Binary
                    : PostgresFormatCode.Text;

                var reader = await command.ExecuteReaderAsync(cancel);
                Assert.IsTrue(await reader.ReadAsync(cancel));

                var date = (DateTime)reader[0];

                Assert.AreEqual(2001, date.Year);
                Assert.AreEqual(9, date.Month);
                Assert.AreEqual(27, date.Day);
                Assert.AreEqual(DateTimeKind.Utc, date.Kind);

                Assert.IsFalse(await reader.ReadAsync(cancel));
            }
        }

        [DataTestMethod]
        [DataRow(false, DisplayName = "Text results")]
        [DataRow(true, DisplayName = "Binary results")]
        public async Task BasicTestGuidTypes(bool useBinary)
        {
            var cancel = CancellationToken.None;

            using (var connection = await PostgresServerInformation.Open())
            using (var command = new PostgresCommand(
                "SELECT 'AC426679-CD6A-4571-A519-C4DD7691C63C'::uuid", connection))
            {
                connection.QueryResultFormat = useBinary
                    ? PostgresFormatCode.Binary
                    : PostgresFormatCode.Text;

                var reader = await command.ExecuteReaderAsync(cancel);
                Assert.IsTrue(await reader.ReadAsync(cancel));

                var guid = (Guid)reader[0];

                Assert.AreEqual(
                    Guid.Parse("AC426679-CD6A-4571-A519-C4DD7691C63C"),
                    guid);

                Assert.IsFalse(await reader.ReadAsync(cancel));
            }
        }

        [DataTestMethod]
        [DataRow(false, DisplayName = "Text results")]
        [DataRow(true, DisplayName = "Binary results")]
        public async Task BasicIntArrayTypes(bool useBinary)
        {
            var cancel = CancellationToken.None;

            using (var connection = await PostgresServerInformation.Open())
            using (var command = new PostgresCommand(
                "select '{10000, 10000, 10000, 10000}'::integer[]", connection))
            {
                connection.QueryResultFormat = useBinary
                    ? PostgresFormatCode.Binary
                    : PostgresFormatCode.Text;

                var reader = await command.ExecuteReaderAsync(cancel);
                Assert.IsTrue(await reader.ReadAsync(cancel));

                var array = (int[])reader[0];

                CollectionAssert.AreEqual(
                    new[] { 10000, 10000, 10000, 10000 },
                    array);

                Assert.IsFalse(await reader.ReadAsync(cancel));
            }
        }
    }
}
