using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AsyncPostgresClient.Tests
{
    [TestClass]
    public class WiresharkBufferTests
    {
        [TestMethod]
        public void TestReadFromStringMultipleLines()
        {
            const string s = @"00000000  00 00 00 4b 00 03 00 00  75 73 65 72 00 64 65 76   ...K.... user.dev
    00000040  73 71 6c 5f 74 65 73 74  73 00 00                  sql_test s..";

            var expected = new byte[] {
                0x00, 0x00, 0x00, 0x4b, 0x00, 0x03, 0x00, 0x00, 0x75, 0x73, 0x65, 0x72, 0x00, 0x64, 0x65, 0x76,
                0x73, 0x71, 0x6c, 0x5f, 0x74, 0x65, 0x73, 0x74, 0x73, 0x00, 0x00
            };

            var actual = WiresharkBuffer.ReadFromString(s);

            CollectionAssert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void TestReadFromStringOpeningNewLines()
        {
            const string s = @"
    00000000  00 00 00 4b 00 03 00 00  75 73 65 72 00 64 65 76   ...K.... user.dev
    00000040  73 71 6c 5f 74 65 73 74  73 00 00                  sql_test s..";

            var expected = new byte[] {
                0x00, 0x00, 0x00, 0x4b, 0x00, 0x03, 0x00, 0x00, 0x75, 0x73, 0x65, 0x72, 0x00, 0x64, 0x65, 0x76,
                0x73, 0x71, 0x6c, 0x5f, 0x74, 0x65, 0x73, 0x74, 0x73, 0x00, 0x00
            };

            var actual = WiresharkBuffer.ReadFromString(s);

            CollectionAssert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void TestReadFromStringSingleLine()
        {
            const string s = @"00000000  00 00 00 4b 00 03 00 00  75 73 65 72 00 64 65 76   ...K.... user.dev";

            var expected = new byte[] {
                0x00, 0x00, 0x00, 0x4b, 0x00, 0x03, 0x00, 0x00, 0x75, 0x73, 0x65, 0x72, 0x00, 0x64, 0x65, 0x76
            };

            var actual = WiresharkBuffer.ReadFromString(s);

            CollectionAssert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void TestReadFromStringShortLine()
        {
            const string s = @"00000040  73 71 6c 5f 74 65 73 74  73 00 00                  sql_test s..";

            var expected = new byte[] {
                0x73, 0x71, 0x6c, 0x5f, 0x74, 0x65, 0x73, 0x74, 0x73, 0x00, 0x00
            };

            var actual = WiresharkBuffer.ReadFromString(s);

            CollectionAssert.AreEqual(expected, actual);
        }
    }
}
