using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lennox.AsyncPostgresClient.Tests
{
    [TestClass]
    public class EncodngTests
    {
        private void DemandAscii(byte[] input, string expected)
        {
            var output = new byte[input.Length * 2];

            HexEncoding.WriteAscii(output, 0, output.Length,
                input, 0, input.Length);

            var actual = Encoding.ASCII.GetString(output);

            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void TestHexEncodingWriteAscii()
        {
            DemandAscii(new byte[] { 0xAA }, "aa");
            DemandAscii(new byte[] { 0xAA, 0xBB }, "aabb");
            DemandAscii(new byte[] {
                    0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f },
                "000102030405060708090a0b0c0d0e0f");
        }
    }
}
