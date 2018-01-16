using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AsyncPostgresClient.Tests
{
    internal abstract class PostgresStreamTest
    {
        protected byte[] Buffer;
        protected int Offset;
        protected int Length;
        protected PostgresReadState ReadState;
        protected PostgresClientState ClientState;

        protected void TestInitialize()
        {
            Offset = 0;
            ReadState = new PostgresReadState();
            ClientState = PostgresClientState.CreateDefault();
        }

        protected void AssertAuthorizationOk()
        {
            var found = PostgresMessage.ReadMessage(
                Buffer, ref Offset, ref Length,
                ref ReadState, ref ClientState, out var message);

            Assert.IsTrue(found);
            Assert.IsTrue(message is AuthenticationMessage);
            using (var authMessage = (AuthenticationMessage)message)
            {
                Assert.AreEqual(AuthenticationMessageType.Ok, authMessage.AuthenticationMessageType);
                Assert.AreEqual(0, authMessage.MD5PasswordSalt);
                Assert.IsNull(authMessage.GSSAuthenticationData);
            }
        }

        protected void AssertParameterStatus(string name, string key)
        {
            var found = PostgresMessage.ReadMessage(
                Buffer, ref Offset, ref Length,
                ref ReadState, ref ClientState, out var message);

            Assert.IsTrue(found);
            Assert.IsTrue(message is ParameterStatusMessage);
            var parameterMessage = (ParameterStatusMessage)message;
            Assert.AreEqual(name, parameterMessage.ParameterName);
            Assert.AreEqual(key, parameterMessage.Value);
        }

        protected void AssertBackendKeyData(int pid, int key)
        {
            var found = PostgresMessage.ReadMessage(
                Buffer, ref Offset, ref Length,
                ref ReadState, ref ClientState, out var message);

            Assert.IsTrue(found);
            Assert.IsTrue(message is BackendKeyDataMessage);
            var dataMessage = (BackendKeyDataMessage)message;
            Assert.AreEqual(pid, dataMessage.ProcessId);
            Assert.AreEqual(key, dataMessage.SecretKey);
        }

        protected void AssertReadyForQuery(TransactionIndicatorType type)
        {
            var found = PostgresMessage.ReadMessage(
                Buffer, ref Offset, ref Length,
                ref ReadState, ref ClientState, out var message);

            Assert.IsTrue(found);
            Assert.IsTrue(message is ReadyForQueryMessage);
            var dataMessage = (ReadyForQueryMessage)message;
            Assert.AreEqual(type, dataMessage.TransactionIndicatorType);
        }

        protected void AssertReadNeedsMoreData()
        {
            var found = PostgresMessage.ReadMessage(
                Buffer, ref Offset, ref Length,
                ref ReadState, ref ClientState, out var message);

            Assert.IsFalse(found);
        }
    }
}