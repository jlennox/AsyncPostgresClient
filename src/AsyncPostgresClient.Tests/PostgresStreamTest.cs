using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

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

        protected void SetBufferFromWiresharkString(string s)
        {
            Buffer = WiresharkBuffer.ReadFromString(s);
            Length = Buffer.Length;
            TestInitialize();
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

        protected void AssertParseComplete()
        {
            var found = PostgresMessage.ReadMessage(
                Buffer, ref Offset, ref Length,
                ref ReadState, ref ClientState, out var message);

            Assert.IsTrue(found);
            Assert.IsTrue(message is ParseCompleteMessage);
        }

        protected void AssertBindComplete()
        {
            var found = PostgresMessage.ReadMessage(
                Buffer, ref Offset, ref Length,
                ref ReadState, ref ClientState, out var message);

            Assert.IsTrue(found);
            Assert.IsTrue(message is BindCompleteMessage);
        }

        protected void AssertRowDescription(
            int fieldCount,
            RowDescriptionField[] descriptions)
        {
            var found = PostgresMessage.ReadMessage(
                Buffer, ref Offset, ref Length,
                ref ReadState, ref ClientState, out var message);

            Assert.IsTrue(found);
            Assert.IsTrue(message is RowDescriptionMessage);
            var dataMessage = (RowDescriptionMessage)message;

            // If used to generate test code, verify the data against known
            // values from wireshark or the like.
            var fields = string.Join(", ", dataMessage.Fields.Select(t =>
                $@"new RowDescriptionField {{
                    Name = ""{t.Name}"",
                    TableObjectId = {t.TableObjectId},
                    AttributeNumber = {t.ColumnIndex},
                    DataTypeObjectId = {t.DataTypeObjectId},
                    DataTypeSize = {t.DataTypeSize},
                    TypeModifier = {t.TypeModifier},
                    FormatCode = {t.FormatCode}
                }}"));

            Assert.AreEqual(fieldCount, dataMessage.FieldCount);

            for (var i = 0; i < fieldCount; ++i)
            {
                var expected = descriptions[i];
                var actual = dataMessage.Fields[i];

                Assert.AreEqual(expected.Name, actual.Name);
                Assert.AreEqual(expected.TableObjectId, actual.TableObjectId);
                Assert.AreEqual(expected.ColumnIndex, actual.ColumnIndex);
                Assert.AreEqual(expected.DataTypeObjectId, actual.DataTypeObjectId);
                Assert.AreEqual(expected.DataTypeSize, actual.DataTypeSize);
                Assert.AreEqual(expected.TypeModifier, actual.TypeModifier);
                Assert.AreEqual(expected.FormatCode, actual.FormatCode);
            }
        }

        protected void AssertDataRow(int columnCount, DataRow[] rows)
        {
            var found = PostgresMessage.ReadMessage(
                Buffer, ref Offset, ref Length,
                ref ReadState, ref ClientState, out var message);

            Assert.IsTrue(found);
            Assert.IsTrue(message is DataRowMessage);
            var dataMessage = (DataRowMessage)message;

            // If used to generate test code, verify the data against known
            // values from wireshark or the like.
            var description = string.Join(",\n", dataMessage.Rows.Select(t =>
                $"new DataRow({t.Length}, new byte[] {{{string.Join(",", t.Data.Select(q => $"0x{q:X2}"))}}})"));

            Assert.AreEqual(columnCount, dataMessage.ColumnCount);
            Assert.AreEqual(columnCount, rows.Length);

            for (var i = 0; i < columnCount; ++i)
            {
                var expected = rows[i];
                var actual = dataMessage.Rows[i];

                Assert.AreEqual(expected.Length, actual.Length);
                AssertArray(expected.Data, actual.Data, actual.Length);
            }
        }

        protected void FastForwardDataRow(int count)
        {
            for (var i = 0; i < count; ++i)
            {
                var found = PostgresMessage.ReadMessage(
                    Buffer, ref Offset, ref Length,
                    ref ReadState, ref ClientState, out var message);

                Assert.IsTrue(found);
                Assert.IsTrue(message is DataRowMessage);
            }
        }

        protected void AssertCommandComplete(string tag)
        {
            var found = PostgresMessage.ReadMessage(
                Buffer, ref Offset, ref Length,
                ref ReadState, ref ClientState, out var message);

            Assert.IsTrue(found);
            Assert.IsTrue(message is CommandCompleteMessage);
            var dataMessage = (CommandCompleteMessage)message;

            Assert.AreEqual(tag, dataMessage.Tag);
        }

        protected void AssertReadNeedsMoreData()
        {
            var found = PostgresMessage.ReadMessage(
                Buffer, ref Offset, ref Length,
                ref ReadState, ref ClientState, out var message);

            Assert.IsFalse(found);
        }

        private static void AssertArray<T>(T[] expected, T[] actual, int length)
        {
            for (var i = 0; i < length; ++i)
            {
                Assert.AreEqual(expected[i], actual[i]);
            }
        }
    }
}