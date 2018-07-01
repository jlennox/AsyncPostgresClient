using System;
using System.Text;
using Lennox.AsyncPostgresClient.PostgresTypes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lennox.AsyncPostgresClient.Tests
{
    [TestClass]
    public class PostgresTypeConvertersTests
    {
        private static void Test<T>(
            byte[] data,
            string postgresTypeName,
            T expected)
        {
            var row = new DataRow(data.Length, data);

            var actual = PostgresTypeConverter.Convert(
                postgresTypeName.GetHashCode(),
                row,
                PostgresFormatCode.Text,
                PostgresClientState.CreateDefault());

            Assert.IsTrue(actual is T);
            Assert.AreEqual(expected, actual);
        }

        private static void Test<T>(
            string data,
            string postgresTypeName,
            T expected)
        {
            Test(Encoding.UTF8.GetBytes(data), postgresTypeName, expected);
        }

        [TestMethod]
        public void BasicTests()
        {
            const string uuid = "E926CF79-5EBD-412F-BB30-722DC3E29901";

            Test("t", PostgresTypeNames.Bool, true);
            Test("f", PostgresTypeNames.Bool, false);
            Test("321", PostgresTypeNames.Int2, (short)321);
            Test("321", PostgresTypeNames.Int4, 321);
            Test("321", PostgresTypeNames.Int8, 321L);
            Test("321", PostgresTypeNames.Float4, (float)321);
            Test("321", PostgresTypeNames.Float8, (float)321);
            Test("321", PostgresTypeNames.Money, (decimal)321);
            Test("321", PostgresTypeNames.Text, "321");
            Test(uuid, PostgresTypeNames.Uuid, new Guid(uuid));
        }
    }
}
