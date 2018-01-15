using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using AsyncPostgresClient.Extension;

namespace AsyncPostgresClient
{
    // https://www.postgresql.org/docs/9.3/static/protocol-message-formats.html
    internal static class PostgresMessage
    {
        private static readonly byte[] _emptyData = new byte[0];

        public static bool ReadMessage(
            byte[] buffer, ref int offset,
            ref int length, ref PostgresReadState state)
        {
            while (length > 0)
            {
                switch (state.Position)
                {
                    case PostgresReadStatePosition.Type:
                        if (!BinaryBuffer.TryReadByte(
                            buffer, ref offset, ref length,
                            ref state.TypeId))
                        {
                            return false;
                        }

                        state.Position = PostgresReadStatePosition.Length;
                        break;
                    case PostgresReadStatePosition.Length:
                        if (!BinaryBuffer.TryReadInt(
                            buffer, ref offset, ref length,
                            ref state.Length, ref state.TryReadState))
                        {
                            return false;
                        }

                        // The count includes its own length in it, so if it's
                        // negative or less than the smallest possible value,
                        // consider it an error.
                        // TODO: Add an upper bounds check. Will help with
                        // protocol desyncs.
                        if (state.Length < 4)
                        {
                            throw new ArgumentOutOfRangeException(
                                nameof(state.Length),
                                state.Length.ToString("X4"),
                                "Invalid message length.");
                        }
                        else if (state.Length == 0)
                        {
                            state.Position = PostgresReadStatePosition.Decode;
                            state.Data = _emptyData;
                        }
                        else
                        {
                            state.Position = PostgresReadStatePosition.Data;
                            state.Data = new byte[state.Length];
                        }

                        break;
                    case PostgresReadStatePosition.Data:
                        // -4 because Length contains itself.
                        var dataLeft = state.Length - state.TryReadState - 4;
                        var dataAvailable = Math.Min(dataLeft, length);

                        Buffer.BlockCopy(buffer, offset, state.Data,
                            state.TryReadState, dataAvailable);

                        if (dataLeft == dataAvailable)
                        {
                            state.Position = PostgresReadStatePosition.Decode;
                            state.TryReadState = 0;
                        }

                        break;
                    case PostgresReadStatePosition.Decode:
                        switch (state.TypeId)
                        {
                            case (byte)'R':
                                break;
                        }
                        break;
                }
            }

            return false;
        }

        internal static byte[] ReadByteArray(BinaryBuffer bb, int length)
        {
            var array = ArrayPool<byte>.GetArray(length);

            bb.CopyTo(array, 0, length);

            return array;
        }

        internal static int[] ReadIntArray(BinaryBuffer bb, short length)
        {
            var array = ArrayPool<int>.GetArray(length);

            for (var i = 0; i < length; ++i)
            {
                array[i] = bb.ReadIntNetwork();
            }

            return array;
        }

        internal static string[] ReadStringArray(
            BinaryBuffer bb, Encoding encoding, short length)
        {
            var array = ArrayPool<string>.GetArray(length);

            for (var i = 0; i < length; ++i)
            {
                array[i] = bb.ReadString(encoding);
            }

            return array;
        }
    }

    internal enum PostgresReadStatePosition
    {
        Type, Length, Data, Decode
    }

    internal struct PostgresReadState
    {
        public PostgresReadStatePosition Position;
        public byte TypeId;
        public int Length;
        public byte[] Data;
        public int TryReadState;
    }

    interface IPostgresMessage
    {
        //int Length { get; set; }

        void Read(ref PostgresClientState state, BinaryBuffer bb, int length);
        void Write(ref PostgresClientState state, MemoryStream ms);
    }

    internal enum AuthenticationMessageType
    {
        Ok = 0,
        KerberosV5 = 2,
        CleartextPassword = 3,
        MD5Password = 5,
        SCMCredential = 6,
        GSS = 7,
        SSPI = 9,
        GSSContinue = 8
    }

    internal enum StatementTargetType
    {
        PreparedStatement = (int)'S',
        Portal = (int)'P'
    }

    internal enum TransactionIndicatorType
    {
        Idle = (byte)'I',
        Transaction = (byte)'T',
        TransactionFailed = (byte)'E'
    }

    internal enum PostgresFormatCode : short
    {
        Text = 0,
        Binary = 1
    }

    internal interface ICopyResponseMessage : IPostgresMessage
    {
        PostgresFormatCode CopyFormatCode { get; set; }
        short ColumnCount { get; set; }
        PostgresFormatCode[] ColumnFormatCodes { get; set; }
    }

    internal static class CopyResponseMessageHandler
    {
        public static void Read<T>(
            ref T message, ref PostgresClientState state,
            BinaryBuffer bb, int length) where T : ICopyResponseMessage
        {
            message.CopyFormatCode = (PostgresFormatCode)bb.ReadByte();
            message.ColumnCount = bb.ReadShortNetwork();

            AssertMessageValue.Length(message.ColumnCount * 2 + 5, length);

            message.ColumnFormatCodes = new PostgresFormatCode[message.ColumnCount];
            for (var i = 0; i < message.ColumnCount; ++i)
            {
                message.ColumnFormatCodes[i] = (PostgresFormatCode)bb.ReadShortNetwork();
            }
        }
    }

    internal static class AssertMessageValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Length(int expected, int actual)
        {
            if (expected != actual)
            {
                throw new ArgumentOutOfRangeException(
                    "length", actual,
                    $"Message length of {expected} expected, but was {actual}");
            }
        }
    }

    internal struct LengthPlacehold
    {
        private readonly MemoryStream _ms;
        private readonly long _lengthPos;

        private LengthPlacehold(MemoryStream ms)
        {
            _ms = ms;
            _lengthPos = ms.Position;
        }

        public static LengthPlacehold Start(MemoryStream ms)
        {
            ms.WriteNetwork(0); // Length placeholder.
            return new LengthPlacehold(ms);
        }

        public void WriteLength(int length)
        {
            var endPos = _ms.Position;
            _ms.Position = _lengthPos;
            _ms.WriteNetwork(length);
            _ms.Position = endPos;
        }
    }

    public class PostgresClientOnlyMessageException : Exception
    {
    }

    public class PostgresServerOnlyMessageException : Exception
    {
    }

    internal struct PostgresClientState
    {
        public Encoding ClientEncoding;
        public Encoding ServerEncoding;

        public static PostgresClientState Default()
        {
            return new PostgresClientState {
                ClientEncoding = Encoding.ASCII,
                ServerEncoding = Encoding.ASCII
            };
        }
    }

    internal struct AuthenticationMessage : IPostgresMessage
    {
        public const byte MessageId = (byte) 'R';

        public AuthenticationMessageType AuthenticationMessageType { get; private set; }

        // AuthenticationMessageType.MD5Password type only.
        public int MD5PasswordSalt { get; private set; }

        // AuthenticationMessageType.GSSContinue type only.
        public byte[] GSSAuthenticationData { get; private set; }

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            AuthenticationMessageType = (AuthenticationMessageType)bb.ReadByte();

            switch (AuthenticationMessageType)
            {
                case AuthenticationMessageType.MD5Password:
                    AssertMessageValue.Length(8, length);
                    MD5PasswordSalt = bb.ReadIntNetwork();
                    break;
                case AuthenticationMessageType.GSSContinue:
                    GSSAuthenticationData = bb.Buffer;
                    break;
                default:
                    AssertMessageValue.Length(4, length);
                    break;
            }
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            throw new PostgresServerOnlyMessageException();
        }
    }

    internal struct BackendKeyDataMessage : IPostgresMessage
    {
        public const byte MessageId = (byte)'K';

        public int ProcessId { get; private set; }
        public int SecretKey { get; private set; }

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            AssertMessageValue.Length(12, length);

            ProcessId = bb.ReadIntNetwork();
            SecretKey = bb.ReadIntNetwork();
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            throw new PostgresServerOnlyMessageException();
        }
    }

    internal struct BindMessage : IPostgresMessage
    {
        public const byte MessageId = (byte)'B';

        public string PortalName { get; set; }
        public string PreparedStatementName { get; set; }
        public short FormatCodeCount { get; set; }
        public short[] FormatCodes { get; set; }
        public short ParameterCount { get; set; }
        public int ParameterValueLength { get; set; }
        public byte[] Parameters { get; set; }
        public short ResultColumnFormatCodeCount { get; set; }
        public short[] ResultColumnFormatCodes { get; set; }

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            throw new PostgresClientOnlyMessageException();
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            ms.WriteByte(MessageId);

            var length = 4 + 2 + 2 + 4 + 2;
            var lengthPos = ms.Position;
            ms.WriteNetwork(0); // Length placeholder.
            length += ms.WriteString(PortalName, state.ClientEncoding);
            length += ms.WriteString(PreparedStatementName, state.ClientEncoding);
            ms.WriteNetwork(FormatCodeCount);
            length += ms.WriteNetwork(FormatCodes);
            ms.WriteNetwork(ParameterCount);
            ms.WriteNetwork(ParameterValueLength);
            length += ms.WriteNetwork(Parameters, ParameterValueLength);
            ms.WriteNetwork(ResultColumnFormatCodeCount);
            length += ms.WriteNetwork(ResultColumnFormatCodes);

            var endPos = ms.Position;
            ms.Position = lengthPos;
            ms.WriteNetwork(length);
            ms.Position = endPos;
        }
    }

    internal struct BindCompleteMessage : IPostgresMessage
    {
        public const byte MessageId = (byte)'2';

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            AssertMessageValue.Length(4, length);
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            throw new PostgresServerOnlyMessageException();
        }
    }

    internal struct CancelRequestMessage : IPostgresMessage
    {
        public int ProcessId { get; set; }
        public int SecretKey { get; set; }

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            throw new PostgresClientOnlyMessageException();
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            // There is on length byte.
            ms.WriteNetwork(16);
            ms.WriteNetwork((int)80877102);
            ms.WriteNetwork(ProcessId);
            ms.WriteNetwork(SecretKey);
        }
    }

    internal struct CloseMessage : IPostgresMessage
    {
        public const byte MessageId = (byte)'C';

        public StatementTargetType StatementTargetType { get; set; }
        public string TargetName { get; set; }

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            throw new PostgresClientOnlyMessageException();
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            ms.WriteByte(MessageId);

            var length = 5;
            var lengthPos = ms.Position;
            ms.WriteNetwork(0); // Length placeholder.
            ms.WriteByte((byte)StatementTargetType);
            length += ms.WriteString(TargetName, state.ClientEncoding);

            var endPos = ms.Position;
            ms.Position = lengthPos;
            ms.WriteNetwork(length);
            ms.Position = endPos;
        }
    }

    internal struct CloseCompleteMessage : IPostgresMessage
    {
        public const byte MessageId = (byte)'3';

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            AssertMessageValue.Length(4, length);
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            throw new PostgresServerOnlyMessageException();
        }
    }

    internal struct CommandCompleteMessage : IPostgresMessage
    {
        public const byte MessageId = (byte)'C';

        public string Tag { get; private set; }

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            Tag = bb.ReadString(state.ServerEncoding);
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            throw new PostgresServerOnlyMessageException();
        }
    }

    internal struct CopyDataMessage : IPostgresMessage, IDisposable
    {
        public const byte MessageId = (byte)'d';

        public int DataByteCount { get; set; }
        public byte[] Data
        {
            get => _data;
            set => _data = value;
        }

        private byte[] _data;
        private bool _pooledArray;

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            _pooledArray = true;
            _data = PostgresMessage.ReadByteArray(bb, length);
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            ms.WriteByte(MessageId);
            ms.WriteNetwork(DataByteCount + 4);
            ms.WriteNetwork(Data, DataByteCount);
        }

        public void Dispose()
        {
            if (_pooledArray)
            {
                ArrayPool.Free(ref _data);
            }
        }
    }

    internal struct CopyDoneMessage : IPostgresMessage
    {
        public const byte MessageId = (byte)'c';

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            AssertMessageValue.Length(4, length);
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            ms.WriteByte(MessageId);
            ms.WriteNetwork(4);
        }
    }

    internal struct CopyFailMessage : IPostgresMessage
    {
        public const byte MessageId = (byte)'f';

        public string ErrorMessage { get; set; }

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            throw new PostgresClientOnlyMessageException();
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            ms.WriteByte(MessageId);

            var length = 4;
            var lengthPos = LengthPlacehold.Start(ms);
            length += ms.WriteString(ErrorMessage, state.ClientEncoding);

            lengthPos.WriteLength(length);
        }
    }

    internal struct CopyInResponse : ICopyResponseMessage
    {
        public const byte MessageId = (byte)'G';

        public PostgresFormatCode CopyFormatCode { get; set; }
        public short ColumnCount { get; set; }
        public PostgresFormatCode[] ColumnFormatCodes { get; set; }

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            CopyResponseMessageHandler.Read(ref this, ref state, bb, length);
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            throw new PostgresServerOnlyMessageException();
        }
    }

    internal struct CopyOutResponse : ICopyResponseMessage
    {
        public const byte MessageId = (byte)'H';

        public PostgresFormatCode CopyFormatCode { get; set; }
        public short ColumnCount { get; set; }
        public PostgresFormatCode[] ColumnFormatCodes { get; set; }

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            CopyResponseMessageHandler.Read(ref this, ref state, bb, length);
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            throw new PostgresServerOnlyMessageException();
        }
    }

    internal struct CopyBothResponse : ICopyResponseMessage
    {
        public const byte MessageId = (byte)'W';

        public PostgresFormatCode CopyFormatCode { get; set; }
        public short ColumnCount { get; set; }
        public PostgresFormatCode[] ColumnFormatCodes { get; set; }

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            CopyResponseMessageHandler.Read(ref this, ref state, bb, length);
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            throw new PostgresServerOnlyMessageException();
        }
    }

    internal struct DataRowMessage : IPostgresMessage, IDisposable
    {
        public const byte MessageId = (byte)'D';

        public short ColumnCount { get; private set; }
        public int ColumnLength { get; private set; }
        public byte[] Data => _data;

        private byte[] _data;

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            ColumnCount = bb.ReadShortNetwork();
            ColumnLength = bb.ReadIntNetwork();

            if (ColumnLength == -1)
            {
                AssertMessageValue.Length(10, length);

                _data = EmptyArray<byte>.Value;
            }
            else
            {
                AssertMessageValue.Length(10 + ColumnLength, length);

                _data = ArrayPool<byte>.GetArray(ColumnLength);
                bb.CopyTo(_data, 0, ColumnLength);
            }
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            throw new PostgresServerOnlyMessageException();
        }

        public void Dispose()
        {
            ArrayPool.Free(ref _data);
        }
    }

    internal struct DescribeMessage : IPostgresMessage
    {
        public const byte MessageId = (byte)'D';

        public StatementTargetType StatementTargetType { get; set; }
        public string TargetName { get; set; }

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            throw new PostgresClientOnlyMessageException();
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            ms.WriteByte(MessageId);

            var length = 5;
            var lengthPos = LengthPlacehold.Start(ms);
            ms.WriteByte((byte)StatementTargetType);
            length += ms.WriteString(TargetName, state.ClientEncoding);

            lengthPos.WriteLength(length);
        }
    }

    internal struct EmptyQueryResponseMessage : IPostgresMessage
    {
        public const byte MessageId = (byte)'I';

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            AssertMessageValue.Length(4, length);
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            throw new PostgresServerOnlyMessageException();
        }
    }

    internal struct FieldValueResponse
    {
        private static List<FieldValueResponse> _responseList;

        public byte FieldType { get; private set; }
        public string Value { get; private set; }

        public int ComputedLength { get; private set; }

        public static FieldValueResponse? Create(
            ref PostgresClientState state, BinaryBuffer bb)
        {
            var type = bb.ReadByte();

            if (type == 0)
            {
                return null;
            }

            return new FieldValueResponse {
                FieldType = type,
                Value = bb.ReadString(state.ServerEncoding, out var sLength),
                ComputedLength = 1 + sLength
            };
        }

        public static FieldValueResponse[] CreateAll(
            ref PostgresClientState state, BinaryBuffer bb,
            out int length, out int count)
        {
            var responseList = Interlocked.Exchange(ref _responseList, null)
                ?? new List<FieldValueResponse>();

            length = 0;
            count = 0;

            while (true)
            {
                var error = Create(ref state, bb);

                if (!error.HasValue)
                {
                    length += 1;
                    break;
                }

                responseList.Add(error.Value);
                length += error.Value.ComputedLength;
                ++count;
            }

            var responses = ArrayPool<FieldValueResponse>
                .GetArray(responseList.Count);
            responseList.CopyTo(responses);

            responseList.Clear();
            Interlocked.CompareExchange(ref _responseList, responseList, null);

            return responses;
        }
    }

    internal struct ErrorResponseMessage : IPostgresMessage, IDisposable
    {
        public const byte MessageId = (byte)'E';

        public int ErrorCount { get; private set; } // Computed
        public FieldValueResponse[] Errors => _errors;

        private FieldValueResponse[] _errors;

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            _errors = FieldValueResponse.CreateAll(
                ref state, bb, out var actualLength, out var count);

            ErrorCount = count;

            AssertMessageValue.Length(actualLength + 4, length);
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            throw new PostgresServerOnlyMessageException();
        }

        public void Dispose()
        {
            ArrayPool.Free(ref _errors);
        }
    }

    internal struct ExecuteMessage : IPostgresMessage
    {
        public const byte MessageId = (byte)'E';

        public string PortalName { get; set; }
        public int RowCountResultMax { get; set; }

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            throw new PostgresClientOnlyMessageException();
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            ms.WriteByte(MessageId);

            var length = 8;
            var lengthPos = LengthPlacehold.Start(ms);
            length += ms.WriteString(PortalName, state.ClientEncoding);
            ms.WriteNetwork(RowCountResultMax);

            lengthPos.WriteLength(length);
        }
    }

    internal struct FlushMessage : IPostgresMessage
    {
        public const byte MessageId = (byte)'H';

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            throw new PostgresClientOnlyMessageException();
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            ms.WriteByte(MessageId);
            ms.WriteNetwork(4);
        }
    }

    internal struct FunctionCallMessage : IPostgresMessage
    {
        public const byte MessageId = (byte)'F';

        public int ObjectId { get; set; }
        public short ArgumentFormatCodeCount { get; set; }
        public PostgresFormatCode[] ArgumentFormatCodes { get; set; } // short[]
        public short ArgumentCount { get; set; }
        public int ArgumentByteCount { get; set; }
        public byte[] ArgumentData { get; set; }
        public PostgresFormatCode ResultFormatCode { get; set; } // short

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            throw new PostgresClientOnlyMessageException();
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            ms.WriteByte(MessageId);
            ms.WriteNetwork(18 + ArgumentFormatCodeCount * 2 + ArgumentByteCount); // Length placeholder.
            ms.WriteNetwork(ObjectId);
            ms.WriteNetwork(ArgumentFormatCodeCount);
            for (var i = 0; i < ArgumentFormatCodeCount; ++i)
            {
                ms.WriteNetwork((short)ArgumentFormatCodes[i]);
            }
            ms.WriteNetwork(ArgumentCount);
            ms.WriteNetwork(ArgumentByteCount);
            ms.WriteNetwork(ArgumentData, ArgumentByteCount);
            ms.WriteNetwork((short)ResultFormatCode);
        }
    }

    internal struct FunctionCallResponseMessage : IPostgresMessage, IDisposable
    {
        public const byte MessageId = (byte)'V';

        public int ResponseByteCount { get; private set; }
        public byte[] Response => _response;

        private byte[] _response;

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            ResponseByteCount = bb.ReadIntNetwork();

            AssertMessageValue.Length(ResponseByteCount + 8, length);

            var response = ArrayPool<byte>.GetArray(ResponseByteCount);
            bb.CopyTo(response, 0, ResponseByteCount);
            _response = response;
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            throw new PostgresServerOnlyMessageException();
        }

        public void Dispose()
        {
            ArrayPool.Free(ref _response);
        }
    }

    internal struct NoDataMessage : IPostgresMessage
    {
        public const byte MessageId = (byte)'n';

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            AssertMessageValue.Length(4, length);
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            throw new PostgresServerOnlyMessageException();
        }
    }

    internal struct NoticeResponseMessage : IPostgresMessage
    {
        public const byte MessageId = (byte)'N';

        public int NoticeCount { get; private set; }
        public FieldValueResponse[] Notices => _notices;

        private FieldValueResponse[] _notices;

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            _notices = FieldValueResponse.CreateAll(
                ref state, bb, out var actualLength, out var count);
            NoticeCount = count;

            AssertMessageValue.Length(actualLength + 4, length);
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            throw new PostgresServerOnlyMessageException();
        }
    }

    internal struct NotificationResponseMessage : IPostgresMessage
    {
        public const byte MessageId = (byte)'A';

        public int ProcessId { get; private set; }
        public string ChannelName { get; private set; }
        public string Payload { get; private set; }

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            var actualLength = 8;
            ProcessId = bb.ReadIntNetwork();
            ChannelName = bb.ReadString(state.ServerEncoding, out var sLength);
            actualLength += sLength;
            Payload = bb.ReadString(state.ServerEncoding, out sLength);
            actualLength += sLength;

            AssertMessageValue.Length(actualLength, length);
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            throw new PostgresServerOnlyMessageException();
        }
    }

    internal struct ParameterDescriptionMessage : IPostgresMessage, IDisposable
    {
        public const byte MessageId = (byte)'t';

        public short ParameterCount { get; private set; }
        public int[] ObjectId => _objectId;

        private int[] _objectId;

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            ParameterCount = bb.ReadShort();
            _objectId = PostgresMessage.ReadIntArray(bb, ParameterCount);
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            throw new PostgresServerOnlyMessageException();
        }

        public void Dispose()
        {
            ArrayPool.Free(ref _objectId);
        }
    }

    internal struct ParameterStatusMessage : IPostgresMessage
    {
        public const byte MessageId = (byte)'S';

        public string ParameterName { get; private set; }
        public string Value { get; private set; }

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            var actualLength = 4;
            int sLength;

            ParameterName = bb.ReadString(state.ServerEncoding, out sLength);
            actualLength += sLength;
            Value = bb.ReadString(state.ServerEncoding, out sLength);
            actualLength += sLength;

            AssertMessageValue.Length(actualLength, length);
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            throw new PostgresServerOnlyMessageException();
        }
    }

    internal struct ParseMessage : IPostgresMessage
    {
        public const byte MessageId = (byte)'P';

        public string DestinationName { get; set; }
        public string Query { get; set; }
        public short ParameterTypeCount { get; set; }
        public int[] ObjectId { get; set; }

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            throw new PostgresClientOnlyMessageException();
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            ms.WriteByte(MessageId);

            var length = 6;
            var lengthPos = LengthPlacehold.Start(ms);
            length += ms.WriteString(DestinationName, state.ClientEncoding);
            length += ms.WriteString(Query, state.ClientEncoding);
            ms.WriteNetwork(ParameterTypeCount);
            length += ms.WriteNetwork(ObjectId, ParameterTypeCount);

            lengthPos.WriteLength(length);
        }
    }

    internal struct ParseCompleteMessage : IPostgresMessage
    {
        public const byte MessageId = (byte)'1';

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            AssertMessageValue.Length(4, length);
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            throw new PostgresServerOnlyMessageException();
        }
    }

    internal struct PasswordMessage : IPostgresMessage
    {
        public const byte MessageId = (byte)'p';

        public string Password { get; private set; }

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            Password = bb.ReadString(state.ServerEncoding, out var sLength);
            AssertMessageValue.Length(4 + sLength, length);
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            throw new PostgresServerOnlyMessageException();
        }
    }

    internal struct PortalSuspendedMessage : IPostgresMessage
    {
        public const byte MessageId = (byte)'s';

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            AssertMessageValue.Length(4, length);
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            throw new PostgresServerOnlyMessageException();
        }
    }

    internal struct QueryMessage : IPostgresMessage
    {
        public const byte MessageId = (byte)'Q';

        public string Query { get; set; }

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            throw new PostgresClientOnlyMessageException();
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            ms.WriteByte(MessageId);

            var length = 4;
            var lengthPos = LengthPlacehold.Start(ms);
            length += ms.WriteString(Query, state.ClientEncoding);

            lengthPos.WriteLength(length);
        }
    }

    internal struct ReadyForQueryMessage : IPostgresMessage
    {
        public const byte MessageId = (byte)'Z';

        public TransactionIndicatorType TransactionIndicatorType { get; private set; } // byte

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            AssertMessageValue.Length(5, length);

            TransactionIndicatorType = (TransactionIndicatorType)bb.ReadByte();
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            throw new PostgresServerOnlyMessageException();
        }
    }

    internal struct RowDescriptionField
    {
        public string Name { get; private set; }
        public int TableObjectId { get; private set; }
        public short AttributeNumber { get; private set; }
        public int DataTypeObjectId { get; private set; }
        public short DataTypeSize { get; private set; }
        public int TypeModifier { get; private set; }
        public PostgresFormatCode FormatCode { get; private set; } // short

        // Computed by us.
        public int ComputedLength { get; private set; }

        internal static RowDescriptionField Create(
            ref PostgresClientState state, BinaryBuffer bb)
        {
            return new RowDescriptionField {
                Name = bb.ReadString(state.ServerEncoding, out var sLength),
                TableObjectId = bb.ReadIntNetwork(),
                AttributeNumber = bb.ReadShortNetwork(),
                DataTypeObjectId = bb.ReadIntNetwork(),
                DataTypeSize = bb.ReadShortNetwork(),
                TypeModifier = bb.ReadIntNetwork(),
                FormatCode = (PostgresFormatCode)bb.ReadShortNetwork(),
                ComputedLength = sLength + 18
            };
        }
    }

    internal struct RowDescriptionMessage : IPostgresMessage, IDisposable
    {
        public const byte MessageId = (byte)'T';

        public short FieldCount { get; private set; }
        public RowDescriptionField[] Fields => _fields;

        private RowDescriptionField[] _fields;

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            FieldCount = bb.ReadShortNetwork();
            _fields = ArrayPool<RowDescriptionField>.GetArray(FieldCount);
            var actualLength = 6;
            for (var i = 0; i < FieldCount; ++i)
            {
                var field = RowDescriptionField.Create(ref state, bb);
                _fields[i] = field;
                actualLength += field.ComputedLength;
            }

            AssertMessageValue.Length(actualLength, length);
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            throw new PostgresServerOnlyMessageException();
        }

        public void Dispose()
        {
            ArrayPool.Free(ref _fields);
        }
    }

    internal struct SSLRequestMessage : IPostgresMessage
    {
        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            throw new PostgresClientOnlyMessageException();
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            // No message id sent.
            ms.WriteNetwork(8);
            ms.WriteNetwork((int)80877103);
        }
    }

    internal struct StartupMessage : IPostgresMessage
    {
        public KeyValuePair<string, string>[] Messages { get; set; }

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            throw new PostgresClientOnlyMessageException();
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            // No message id sent.
            ms.WriteNetwork(8);
            ms.WriteNetwork((int)196608);

            var messages = Messages;

            if (messages != null)
            {
                for (var i = 0; i < messages.Length; ++i)
                {
                    var message = messages[i];

                    ms.WriteString(message.Key, state.ClientEncoding);
                    ms.WriteString(message.Value, state.ClientEncoding);
                }
            }

            ms.WriteByte(0);
        }
    }

    internal struct SyncMessage : IPostgresMessage
    {
        public const byte MessageId = (byte)'S';

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            throw new PostgresClientOnlyMessageException();
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            ms.WriteByte(MessageId);
            ms.WriteNetwork(4);
        }
    }

    internal struct TerminateMessage : IPostgresMessage
    {
        public const byte MessageId = (byte)'X';

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            throw new PostgresClientOnlyMessageException();
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            ms.WriteByte(MessageId);
            ms.WriteNetwork(4);
        }
    }
}
