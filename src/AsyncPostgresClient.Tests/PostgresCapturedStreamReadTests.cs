using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AsyncPostgresClient.Tests
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
