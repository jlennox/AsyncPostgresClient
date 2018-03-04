using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Lennox.AsyncPostgresClient.Extension;

namespace Lennox.AsyncPostgresClient.Tests
{
    [TestClass]
    public class StringBuilderExTests
    {
        [DataTestMethod]
        [DataRow("", "")]
        [DataRow(" a ", "a")]
        [DataRow(" a", "a")]
        [DataRow("a ", "a")]
        [DataRow("a", "a")]
        [DataRow("   ", "")]
        public void TestTrimming(string input, string expected)
        {
            var sb = new StringBuilder(input);

            Assert.AreEqual(expected, sb.ToStringTrim());
        }
    }
}
