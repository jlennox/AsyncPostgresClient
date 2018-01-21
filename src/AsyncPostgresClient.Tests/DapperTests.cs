using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AsyncPostgresClient.Tests
{
    [TestClass]
    public class DapperTests
    {
        [TestMethod]
        public async Task SelectOne()
        {
            using (var connection = PostgresServerInformation.Open())
            {
                var one = await connection.QueryAsync<int>("SELECT 1");

                Assert.AreEqual(1, one);
            }
        }
    }
}
