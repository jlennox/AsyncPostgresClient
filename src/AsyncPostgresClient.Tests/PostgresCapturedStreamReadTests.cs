using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lennox.AsyncPostgresClient.Tests
{
    [TestClass]
    public class PostgresCapturedStreamReadTests
    {
    }

    internal class PostgresCapturedReadStream : PostgresStreamReadTest
    {
    }

    internal class PostgresStreamReadTest
    {
        protected MemoryStream MemoryStream;

        private void Initialize()
        {
            MemoryStream = new MemoryStream();
        }

        private void CheckEquality()
        {

        }
    }
}
