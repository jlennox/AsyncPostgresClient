using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lennox.AsyncPostgresClient.Tests
{
    [TestClass]
    public class PostgresPropertySettingTests
    {
        [TestMethod]
        public async Task EnsureGetAllWorks()
        {
            var cancel = CancellationToken.None;
             
            using (var connection = await PostgresServerInformation.Open())
            {
                var properties = await PostgresPropertySetting.GetAll(
                    true, connection, cancel);

                Assert.IsTrue(properties.Count > 200);

                var backslashQuote = properties.FirstOrDefault(
                    t => t.Name == PostgresProperties.BackslashQuote);

                var possibleValues = new[] { "on", "off", "safe_encoding" };

                CollectionAssert.Contains(possibleValues, backslashQuote.Value);
            }
        }
    }
}
