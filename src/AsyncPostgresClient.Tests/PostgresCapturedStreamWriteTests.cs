using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AsyncPostgresClient.Tests
{
    // Tests performed against live captures of Postgres streams captured
    // using Wireshark.
    [TestClass]
    public class PostgresCapturedStreamWriteTests
    {
        [TestMethod]
        public void TestAuthAndParameters()
        {
            var test = new PostgresCapturedWriteStream();
            test.TestAuthAndParameters();
        }

        [TestMethod]
        public void TestDataResult()
        {
            var test = new PostgresCapturedWriteStream();
            test.TestDataResult();
        }

        [TestMethod]
        public void TestCommandComplete()
        {
            var test = new PostgresCapturedWriteStream();
            test.TestCommandComplete();
        }
    }

    // These tests can not directly be [TestMethod]'s because test classes
    // must be marked public, and this would require most of the internal
    // types in AsyncPostgresClient to require to be public.
    internal class PostgresCapturedWriteStream : PostgresStreamTest
    {
        public PostgresCapturedWriteStream()
        {
            TestInitialize();
        }

        public void TestAuthAndParameters()
        {
            const string s = @"
    0000000D  52 00 00 00 08 00 00 00  00 53 00 00 00 16 61 70   R....... .S....ap
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

            SetBufferFromWiresharkString(s);

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

        public void TestDataResult()
        {
            const string s = @"
    00000161  31 00 00 00 04 32 00 00  00 04 54 00 00 00 d0 00   1....2.. ..T.....
    00000171  08 6e 73 70 6e 61 6d 65  00 00 00 0a 37 00 01 00   .nspname ....7...
    00000181  00 00 13 00 40 ff ff ff  ff 00 00 74 79 70 6e 61   ....@... ...typna
    00000191  6d 65 00 00 00 04 df 00  01 00 00 00 13 00 40 ff   me...... ......@.
    000001A1  ff ff ff 00 00 6f 69 64  00 00 00 04 df ff fe 00   .....oid ........
    000001B1  00 00 1a 00 04 ff ff ff  ff 00 00 74 79 70 72 65   ........ ...typre
    000001C1  6c 69 64 00 00 00 04 df  00 0b 00 00 00 1a 00 04   lid..... ........
    000001D1  ff ff ff ff 00 00 74 79  70 62 61 73 65 74 79 70   ......ty pbasetyp
    000001E1  65 00 00 00 04 df 00 18  00 00 00 1a 00 04 ff ff   e....... ........
    000001F1  ff ff 00 00 74 79 70 65  00 00 00 00 00 00 00 00   ....type ........
    00000201  00 00 12 00 01 ff ff ff  ff 00 00 65 6c 65 6d 6f   ........ ...elemo
    00000211  69 64 00 00 00 00 00 00  00 00 00 00 1a 00 04 ff   id...... ........
    00000221  ff ff ff 00 00 6f 72 64  00 00 00 00 00 00 00 00   .....ord ........
    00000231  00 00 17 00 04 ff ff ff  ff 00 00 44 00 00 00 3f   ........ ...D...?
    00000241  00 08 00 00 00 0a 70 67  5f 63 61 74 61 6c 6f 67   ......pg _catalog
    00000251  00 00 00 07 75 6e 6b 6e  6f 77 6e 00 00 00 03 37   ....unkn own....7
    00000261  30 35 00 00 00 01 30 00  00 00 01 30 00 00 00 01   05....0. ...0....
    00000271  62 00 00 00 01 30 00 00  00 01 30 44 00 00 00 42   b....0.. ..0D...B
    00000281  00 08 00 00 00 0a 70 67  5f 63 61 74 61 6c 6f 67   ......pg _catalog
    00000291  00 00 00 09 74 69 6d 65  73 74 61 6d 70 00 00 00   ....time stamp...
    000002A1  04 31 31 31 34 00 00 00  01 30 00 00 00 01 30 00   .1114... .0....0.
    000002B1  00 00 01 62 00 00 00 01  30 00 00 00 01 30 44 00   ...b.... 0....0D.
    000002C1  00 00 3c 00 08 00 00 00  0a 70 67 5f 63 61 74 61   ..<..... .pg_cata
    000002D1  6c 6f 67 00 00 00 04 69  6e 65 74 00 00 00 03 38   log....i net....8
    000002E1  36 39 00 00 00 01 30 00  00 00 01 30 00 00 00 01   69....0. ...0....
    000002F1  62 00 00 00 01 30 00 00  00 01 30 44 00 00 00 3c   b....0.. ..0D...<
    00000301  00 08 00 00 00 0a 70 67  5f 63 61 74 61 6c 6f 67   ......pg _catalog
    00000311  00 00 00 04 63 69 64 72  00 00 00 03 36 35 30 00   ....cidr ....650.
    00000321  00 00 01 30 00 00 00 01  30 00 00 00 01 62 00 00   ...0.... 0....b..
    00000331  00 01 30 00 00 00 01 30  44 00 00 00 3b 00 08 00   ..0....0 D...;...
    00000341  00 00 0a 70 67 5f 63 61  74 61 6c 6f 67 00 00 00   ...pg_ca talog...
    00000351  04 69 6e 74 34 00 00 00  02 32 33 00 00 00 01 30   .int4... .23....0
    00000361  00 00 00 01 30 00 00 00  01 62 00 00 00 01 30 00   ....0... .b....0.
    00000371  00 00 01 30 44 00 00 00  44 00 08 00 00 00 0a 70   ...0D... D......p
    00000381  67 5f 63 61 74 61 6c 6f  67 00 00 00 0b 74 69 6d   g_catalo g....tim
    00000391  65 73 74 61 6d 70 74 7a  00 00 00 04 31 31 38 34   estamptz ....1184
    000003A1  00 00 00 01 30 00 00 00  01 30 00 00 00 01 62 00   ....0... .0....b.
    000003B1  00 00 01 30 00 00 00 01  30 44 00 00 00 3e 00 08   ...0.... 0D...>..
    000003C1  00 00 00 0a 70 67 5f 63  61 74 61 6c 6f 67 00 00   ....pg_c atalog..
    000003D1  00 07 72 65 67 70 72 6f  63 00 00 00 02 32 34 00   ..regpro c....24.
    000003E1  00 00 01 30 00 00 00 01  30 00 00 00 01 62 00 00   ...0.... 0....b..
    000003F1  00 01 30 00 00 00 01 30  44 00 00 00 41 00 08 00   ..0....0 D...A...
    00000401  00 00 0a 70 67 5f 63 61  74 61 6c 6f 67 00 00 00   ...pg_ca talog...
    00000411  08 69 6e 74 65 72 76 61  6c 00 00 00 04 31 31 38   .interva l....118
    00000421  36 00 00 00 01 30 00 00  00 01 30 00 00 00 01 62   6....0.. ..0....b
    00000431  00 00 00 01 30 00 00 00  01 30 44 00 00 00 3b 00   ....0... .0D...;.
    00000441  08 00 00 00 0a 70 67 5f  63 61 74 61 6c 6f 67 00   .....pg_ catalog.
    00000451  00 00 04 74 65 78 74 00  00 00 02 32 35 00 00 00   ...Text. ...25...
    00000461  01 30 00 00 00 01 30 00  00 00 01 62 00 00 00 01   .0....0. ...b....
    00000471  30 00 00 00 01 30 44 00  00 00 3a 00 08 00 00 00   0....0D. ..:.....
    00000481  0a 70 67 5f 63 61 74 61  6c 6f 67 00 00 00 03 6f   .pg_cata log....o
    00000491  69 64 00 00 00 02 32 36  00 00 00 01 30 00 00 00   id....26 ....0...
    000004A1  01 30 00 00 00 01 62 00  00 00 01 30 00 00 00 01   .0....b. ...0....
    000004B1  30 44 00 00 00 3f 00 08  00 00 00 0a 70 67 5f 63   0D...?.. ....pg_c
    000004C1  61 74 61 6c 6f 67 00 00  00 06 74 69 6d 65 74 7a   atalog.. ..timetz
    000004D1  00 00 00 04 31 32 36 36  00 00 00 01 30 00 00 00   ....1266 ....0...
    000004E1  01 30 00 00 00 01 62 00  00 00 01 30 00 00 00 01   .0....b. ...0....
    000004F1  30 44 00 00 00 3a 00 08  00 00 00 0a 70 67 5f 63   0D...:.. ....pg_c
    00000501  61 74 61 6c 6f 67 00 00  00 03 74 69 64 00 00 00   atalog.. ..tid...
    00000511  02 32 37 00 00 00 01 30  00 00 00 01 30 00 00 00   .27....0 ....0...
    00000521  01 62 00 00 00 01 30 00  00 00 01 30 44 00 00 00   .b....0. ...0D...
    00000531  3c 00 08 00 00 00 0a 70  67 5f 63 61 74 61 6c 6f   <......p g_catalo
    00000541  67 00 00 00 03 62 69 74  00 00 00 04 31 35 36 30   g....bit ....1560
    00000551  00 00 00 01 30 00 00 00  01 30 00 00 00 01 62 00   ....0... .0....b.
    00000561  00 00 01 30 00 00 00 01  30 44 00 00 00 3a 00 08   ...0.... 0D...:..
    00000571  00 00 00 0a 70 67 5f 63  61 74 61 6c 6f 67 00 00   ....pg_c atalog..
    00000581  00 03 78 69 64 00 00 00  02 32 38 00 00 00 01 30   ..xid... .28....0
    00000591  00 00 00 01 30 00 00 00  01 62 00 00 00 01 30 00   ....0... .b....0.
    000005A1  00 00 01 30 44 00 00 00  3f 00 08 00 00 00 0a 70   ...0D... ?......p
    000005B1  67 5f 63 61 74 61 6c 6f  67 00 00 00 06 76 61 72   g_catalo g....var
    000005C1  62 69 74 00 00 00 04 31  35 36 32 00 00 00 01 30   bit....1 562....0
    000005D1  00 00 00 01 30 00 00 00  01 62 00 00 00 01 30 00   ....0... .b....0.
    000005E1  00 00 01 30 44 00 00 00  3a 00 08 00 00 00 0a 70   ...0D... :......p
    000005F1  67 5f 63 61 74 61 6c 6f  67 00 00 00 03 63 69 64   g_catalo g....cid
    00000601  00 00 00 02 32 39 00 00  00 01 30 00 00 00 01 30   ....29.. ..0....0
    00000611  00 00 00 01 62 00 00 00  01 30 00 00 00 01 30 44   ....b... .0....0D
    00000621  00 00 00 40 00 08 00 00  00 0a 70 67 5f 63 61 74   ...@.... ..pg_cat
    00000631  61 6c 6f 67 00 00 00 07  6e 75 6d 65 72 69 63 00   alog.... numeric.
    00000641  00 00 04 31 37 30 30 00  00 00 01 30 00 00 00 01   ...1700. ...0....
    00000651  30 00 00 00 01 62 00 00  00 01 30 00 00 00 01 30   0....b.. ..0....0
    00000661  44 00 00 00 42 00 08 00  00 00 0a 70 67 5f 63 61   D...B... ...pg_ca
    00000671  74 61 6c 6f 67 00 00 00  09 72 65 66 63 75 72 73   talog... .refcurs
    00000681  6f 72 00 00 00 04 31 37  39 30 00 00 00 01 30 00   or....17 90....0.
    00000691  00 00 01 30 00 00 00 01  62 00 00 00 01 30 00 00   ...0.... b....0..
    000006A1  00 01 30 44 00 00 00 3c  00 08 00 00 00 0a 70 67   ..0D...< ......pg
    000006B1  5f 63 61 74 61 6c 6f 67  00 00 00 05 62 79 74 65   _catalog ....byte
    000006C1  61 00 00 00 02 31 37 00  00 00 01 30 00 00 00 01   a....17. ...0....
    000006D1  30 00 00 00 01 62 00 00  00 01 30 00 00 00 01 30   0....b.. ..0....0
    000006E1  44 00 00 00 45 00 08 00  00 00 0a 70 67 5f 63 61   D...E... ...pg_ca
    000006F1  74 61 6c 6f 67 00 00 00  0c 72 65 67 70 72 6f 63   talog... .regproc
    00000701  65 64 75 72 65 00 00 00  04 32 32 30 32 00 00 00   edure... .2202...
    00000711  01 30 00 00                                        .0..";

            SetBufferFromWiresharkString(s);

            AssertParseComplete();
            AssertBindComplete();

            AssertRowDescription(8, new[] {
                new RowDescriptionField {
                    Name = "nspname",
                    TableObjectId = 2615,
                    ColumnIndex = 1,
                    DataTypeObjectId = 19,
                    DataTypeSize = 64,
                    TypeModifier = -1,
                    FormatCode = PostgresFormatCode.Text
                }, new RowDescriptionField {
                    Name = "typname",
                    TableObjectId = 1247,
                    ColumnIndex = 1,
                    DataTypeObjectId = 19,
                    DataTypeSize = 64,
                    TypeModifier = -1,
                    FormatCode = PostgresFormatCode.Text
                }, new RowDescriptionField {
                    Name = "oid",
                    TableObjectId = 1247,
                    ColumnIndex = -2,
                    DataTypeObjectId = 26,
                    DataTypeSize = 4,
                    TypeModifier = -1,
                    FormatCode = PostgresFormatCode.Text
                }, new RowDescriptionField {
                    Name = "typrelid",
                    TableObjectId = 1247,
                    ColumnIndex = 11,
                    DataTypeObjectId = 26,
                    DataTypeSize = 4,
                    TypeModifier = -1,
                    FormatCode = PostgresFormatCode.Text
                }, new RowDescriptionField {
                    Name = "typbasetype",
                    TableObjectId = 1247,
                    ColumnIndex = 24,
                    DataTypeObjectId = 26,
                    DataTypeSize = 4,
                    TypeModifier = -1,
                    FormatCode = PostgresFormatCode.Text
                }, new RowDescriptionField {
                    Name = "type",
                    TableObjectId = 0,
                    ColumnIndex = 0,
                    DataTypeObjectId = 18,
                    DataTypeSize = 1,
                    TypeModifier = -1,
                    FormatCode = PostgresFormatCode.Text
                }, new RowDescriptionField {
                    Name = "elemoid",
                    TableObjectId = 0,
                    ColumnIndex = 0,
                    DataTypeObjectId = 26,
                    DataTypeSize = 4,
                    TypeModifier = -1,
                    FormatCode = PostgresFormatCode.Text
                }, new RowDescriptionField {
                    Name = "ord",
                    TableObjectId = 0,
                    ColumnIndex = 0,
                    DataTypeObjectId = 23,
                    DataTypeSize = 4,
                    TypeModifier = -1,
                    FormatCode = PostgresFormatCode.Text
                },
            });

            AssertDataRow(8, new DataRow[] {
                new DataRow(10, new byte[] {0x70,0x67,0x5F,0x63,0x61,0x74,0x61,0x6C,0x6F,0x67}),
                new DataRow(7, new byte[] {0x75,0x6E,0x6B,0x6E,0x6F,0x77,0x6E}),
                new DataRow(3, new byte[] {0x37,0x30,0x35}),
                new DataRow(1, new byte[] {0x30}),
                new DataRow(1, new byte[] {0x30}),
                new DataRow(1, new byte[] {0x62}),
                new DataRow(1, new byte[] {0x30}),
                new DataRow(1, new byte[] {0x30})
            });

            FastForwardDataRow(18);
            AssertReadNeedsMoreData();
        }

        public void TestCommandComplete()
        {
            const string s = @"
    00000000  44 00 00 00 46 00 08 00  00 00 0A 70 67 5F 63 61   D...F......pg_ca
    00000010  74 61 6C 6F 67 00 00 00  0A 5F 69 6E 74 38 72 61   talog...._int8ra
    00000020  6E 67 65 00 00 00 04 33  39 32 37 00 00 00 01 30   nge....3927....0
    00000030  00 00 00 01 30 00 00 00  01 61 00 00 00 04 33 39   ....0....a....39
    00000040  32 36 00 00 00 01 33                              26....3
";

            SetBufferFromWiresharkString(s);

            FastForwardDataRow(1);
            AssertCommandComplete("SELECT 143");
            AssertReadyForQuery(TransactionIndicatorType.Idle);
            AssertReadNeedsMoreData();
        }
    }
}
