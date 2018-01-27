using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lennox.AsyncPostgresClient.Tests.Protocol
{
    [TestClass]
    public class PasswordMessageTests
    {
        [TestMethod]
        public void TestPasswordMessageCreateMd5()
        {
            var salt = new byte[] { 0x77, 0x16, 0x3e, 0xe3 };

            var authMessage = new AuthenticationMessage {
                AuthenticationMessageType = AuthenticationMessageType.MD5Password,
                DataBuffer = salt
            };

            var state = PostgresClientState.CreateDefault();

            var connectionString = new PostgresConnectionString(
                PostgresServerInformation.FakeConnectionString);

            var passwordMessage = PasswordMessage
                .CreateMd5(authMessage, state, connectionString);

            var expectedHash = Encoding.ASCII.GetBytes(
                "md56727bbde9524c2dc683c7274a7ee24a8");

            Assert.AreEqual(0x23, passwordMessage.PasswordLength);
            CollectionAssert.AreEqual(expectedHash, passwordMessage.Password);
        }
    }
}
