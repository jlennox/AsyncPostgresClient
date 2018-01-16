using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AsyncPostgresClient.Tests
{
    // Tests performed against live captures of Postgres streams captured
    // using Wireshark.
    [TestClass]
    public class PostgresCapturedStreamTests
    {
        [TestMethod]
        public void TestBasicStreamDecode()
        {
            var test = new PostgresCapturedStream();
            test.TestBasicStreamDecode();
        }
    }

    // These tests can not directly be [TestMethod]'s because test classes
    // must be marked public, and this would require most of the internal
    // types in AsyncPostgresClient to require to be public.
    internal class PostgresCapturedStream : PostgresStreamTest
    {
        public PostgresCapturedStream()
        {
            TestInitialize();
        }

        public void TestBasicStreamDecode()
        {
            const string s = @"0000000D  52 00 00 00 08 00 00 00  00 53 00 00 00 16 61 70   R....... .S....ap
    0000001D  70 6c 69 63 61 74 69 6f  6e 5f 6e 61 6d 65 00 00   plicatio n_name..
    0000002D  53 00 00 00 19 63 6c 69  65 6e 74 5f 65 6e 63 6f   S....cli ent_enco
    0000003D  64 69 6e 67 00 55 54 46  38 00 53 00 00 00 17 44   ding.UTF 8.S....D
    0000004D  61 74 65 53 74 79 6c 65  00 49 53 4f 2c 20 4d 44   ateStyle .ISO, MD
    0000005D  59 00 53 00 00 00 19 69  6e 74 65 67 65 72 5f 64   Y.S....i nteger_d
    0000006D  61 74 65 74 69 6d 65 73  00 6f 6e 00 53 00 00 00   atetimes .on.S...
    0000007D  1b 49 6e 74 65 72 76 61  6c 53 74 79 6c 65 00 70   .Interva lStyle.p
    0000008D  6f 73 74 67 72 65 73 00  53 00 00 00 15 69 73 5f   ostgres. S....is_
    0000009D  73 75 70 65 72 75 73 65  72 00 6f 66 66 00 53 00   superuse r.off.S.
    000000AD  00 00 1e 73 65 72 76 65  72 5f 65 6e 63 6f 64 69   ...serve r_encodi
    000000BD  6e 67 00 53 51 4c 5f 41  53 43 49 49 00 53 00 00   ng.SQL_A SCII.S..
    000000CD  00 19 73 65 72 76 65 72  5f 76 65 72 73 69 6f 6e   ..server _version
    000000DD  00 39 2e 35 2e 34 00 53  00 00 00 2c 73 65 73 73   .9.5.4.S ...,sess
    000000ED  69 6f 6e 5f 61 75 74 68  6f 72 69 7a 61 74 69 6f   ion_auth orizatio
    000000FD  6e 00 68 65 6c 6c 6f 77  6f 72 6c 64 64 61 74 61   n.hellow orlddata
    0000010D  61 70 70 00 53 00 00 00  23 73 74 61 6e 64 61 72   app.S... #standar
    0000011D  64 5f 63 6f 6e 66 6f 72  6d 69 6e 67 5f 73 74 72   d_confor ming_str
    0000012D  69 6e 67 73 00 6f 6e 00  53 00 00 00 18 54 69 6d   ings.on. S....Tim
    0000013D  65 5a 6f 6e 65 00 55 53  2f 50 61 63 69 66 69 63   eZone.US /Pacific
    0000014D  00 4b 00 00 00 0c 00 00  4f de 6b c1 66 a2 5a 00   .K...... O.k.f.Z.
    0000015D  00 00 05 49                                        ...I";

            Buffer = WiresharkBuffer.ReadFromString(s);
            Length = Buffer.Length;

            AssertAuthorizationOk();
            AssertParameterStatus("application_name", "");
            AssertParameterStatus("client_encoding", "UTF8");
            AssertParameterStatus("DateStyle", "ISO, MDY");
            AssertParameterStatus("integer_datetimes", "on");
            AssertParameterStatus("IntervalStyle", "postgres");
            AssertParameterStatus("is_superuser", "off");
            AssertParameterStatus("server_encoding", "SQL_ASCII");
            AssertParameterStatus("server_version", "9.5.4");
            AssertParameterStatus("session_authorization", "helloworlddataapp");
            AssertParameterStatus("standard_conforming_strings", "on");
            AssertParameterStatus("TimeZone", "US/Pacific");
            AssertBackendKeyData(20446, 1807836834);
            AssertReadyForQuery(TransactionIndicatorType.Idle);
            AssertReadNeedsMoreData();
        }
    }
}
