using System.Text;
using Lennox.AsyncPostgresClient.Extension;
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

            const string expectedHash = "md583ce447ce89d7e4e943205ff8f82f76a";

            try
            {
                var actualHash = Encoding.ASCII.GetString(
                    passwordMessage.Password, 0,
                    passwordMessage.PasswordLength);
                Assert.AreEqual(0x23, passwordMessage.PasswordLength);
                Assert.AreEqual(expectedHash, actualHash);
            }
            finally
            {
                authMessage.TryDispose();
                passwordMessage.TryDispose();
            }
        }
    }
}
