using System;
using System.Collections.Generic;
using System.Text;
using Lennox.AsyncPostgresClient.BufferAccess;
using Lennox.AsyncPostgresClient.Diagnostic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lennox.AsyncPostgresClient.Tests
{
    [TestClass]
    public class NumericBufferTests
    {
        private static void AssertAsciiToInt(string input, int expected)
        {
            var buf = Encoding.ASCII.GetBytes(input);
            var actual = NumericBuffer.AsciiToInt(buf);

            Assert.AreEqual(expected, actual, $"Failed '{input}'");
        }

        private static void AssertAsciiToLong(string input, long expected)
        {
            var buf = Encoding.ASCII.GetBytes(input);
            var actual = NumericBuffer.AsciiToLong(buf);

            Assert.AreEqual(expected, actual, $"Failed '{input}'");
        }

        private static void AssertAsciiToDecimal(string input, decimal expected)
        {
            var buf = Encoding.ASCII.GetBytes(input);
            var actual = NumericBuffer.AsciiToDecimal(buf);

            Assert.AreEqual(expected, actual, $"Failed '{input}'");
        }

        [TestMethod]
        public void TestAsciiToInt()
        {
            AssertAsciiToInt("", 0);
            AssertAsciiToInt("5", 5);
            AssertAsciiToInt("36", 36);
            AssertAsciiToInt("346", 346);
            AssertAsciiToInt("3246", 3246);
            AssertAsciiToInt("31246", 31246);
            AssertAsciiToInt("301246", 301246);
            AssertAsciiToInt("3012416", 3012416);
            AssertAsciiToInt("30124186", 30124186);
            AssertAsciiToInt("301241896", 301241896);
            AssertAsciiToInt("1301241896", 1301241896);
        }

        [TestMethod]
        public void TestAsciiToLong()
        {
            AssertAsciiToLong("", 0L);
            AssertAsciiToLong("5", 5L);
            AssertAsciiToLong("36", 36L);
            AssertAsciiToLong("346", 346L);
            AssertAsciiToLong("3246", 3246L);
            AssertAsciiToLong("31246", 31246L);
            AssertAsciiToLong("301246", 301246L);
            AssertAsciiToLong("3012416", 3012416L);
            AssertAsciiToLong("30124186", 30124186L);
            AssertAsciiToLong("301241896", 301241896L);
            AssertAsciiToLong("1301241896", 1301241896L);
            AssertAsciiToLong("12301241896", 12301241896L);
            AssertAsciiToLong("123012471896", 123012471896L);
            AssertAsciiToLong("1823012471896", 1823012471896L);
            AssertAsciiToLong("18230124718962", 18230124718962L);
            AssertAsciiToLong("182303124718962", 182303124718962L);
            AssertAsciiToLong("1823036124718962", 1823036124718962L);
            AssertAsciiToLong("18230361243718962", 18230361243718962L);
            AssertAsciiToLong("182308361243718962", 182308361243718962L);
        }

        [TestMethod]
        public void TestAsciiToDecimal()
        {
            DebugLogger.Enabled = true;

            AssertAsciiToDecimal("", 0m);
            AssertAsciiToDecimal(".", 0m);
            AssertAsciiToDecimal("5", 5m);
            AssertAsciiToDecimal("5.", 5m);
            AssertAsciiToDecimal(".5", .5m);
            AssertAsciiToDecimal("32.46", 32.46m);
            AssertAsciiToDecimal("13012.41896", 13012.41896m);
            AssertAsciiToDecimal("18.230124718962", 18.230124718962m);
            AssertAsciiToDecimal("18.230124718962+05", 1823012.4718962m);
            AssertAsciiToDecimal("18.230124718962e+05", 1823012.4718962m);

            AssertAsciiToDecimal("1.23457e+08", 123457000m);
        }
    }
}
