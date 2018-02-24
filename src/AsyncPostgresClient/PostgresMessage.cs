using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Lennox.AsyncPostgresClient.Extension;
using Lennox.AsyncPostgresClient.Pool;

namespace Lennox.AsyncPostgresClient
{
    // https://www.postgresql.org/docs/9.3/static/protocol-message-formats.html
    internal static class PostgresMessage
    {
        public static bool ReadMessage(
            byte[] buffer, ref int offset,
            ref int length, ref PostgresReadState state,
            ref PostgresClientState clientState,
            // Arg, I hate this struct boxing.
            out IPostgresMessage message)
        {
            message = null;

            while (length > 0)
            {
                switch (state.Position)
                {
                    case PostgresReadStatePosition.Start:
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
                        if (!BinaryBuffer.TryReadIntNetwork(
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
                        else if (state.Length == 4)
                        {
                            state.Data = EmptyArray<byte>.Value;
                            goto case PostgresReadStatePosition.Decode;
                        }
                        else
                        {
                            state.Position = PostgresReadStatePosition.Data;
                            state.DataLeft = state.Length - 4;
                            state.Data = new byte[state.DataLeft];
                        }

                        break;
                    case PostgresReadStatePosition.Data:
                        // -4 because Length contains itself.
                        var dataLeft = state.DataLeft;
                        var dataAvailable = Math.Min(dataLeft, length);

                        Buffer.BlockCopy(buffer, offset, state.Data,
                            state.Length - state.DataLeft - 4, dataAvailable);

                        length -= dataAvailable;
                        offset += dataAvailable;

                        if (dataLeft == dataAvailable)
                        {
                            goto case PostgresReadStatePosition.Decode;
                        }

                        state.DataLeft -= dataAvailable;

                        break;
                    case PostgresReadStatePosition.Decode:
                        switch (state.TypeId)
                        {
                            case AuthenticationMessage.MessageId:
                                message = new AuthenticationMessage();
                                break;
                            case BackendKeyDataMessage.MessageId:
                                message = new BackendKeyDataMessage();
                                break;
                            case BindCompleteMessage.MessageId:
                                message = new BindCompleteMessage();
                                break;
                            case CloseCompleteMessage.MessageId:
                                message = new CloseCompleteMessage();
                                break;
                            case CommandCompleteMessage.MessageId:
                                message = new CommandCompleteMessage();
                                break;
                            case CopyInResponseMessage.MessageId:
                                message = new CopyInResponseMessage();
                                break;
                            case CopyOutResponseMessage.MessageId:
                                message = new CopyOutResponseMessage();
                                break;
                            case CopyBothResponseMessage.MessageId:
                                message = new CopyBothResponseMessage();
                                break;
                            case DataRowMessage.MessageId:
                                message = new DataRowMessage();
                                break;
                            case EmptyQueryResponseMessage.MessageId:
                                message = new EmptyQueryResponseMessage();
                                break;
                            case ErrorResponseMessage.MessageId:
                                message = new ErrorResponseMessage();
                                break;
                            case FunctionCallResponseMessage.MessageId:
                                message = new FunctionCallResponseMessage();
                                break;
                            case NoDataMessage.MessageId:
                                message = new NoDataMessage();
                                break;
                            case NoticeResponseMessage.MessageId:
                                message = new NoticeResponseMessage();
                                break;
                            case NotificationResponseMessage.MessageId:
                                message = new NotificationResponseMessage();
                                break;
                            case ParameterDescriptionMessage.MessageId:
                                message = new ParameterDescriptionMessage();
                                break;
                            case ParameterStatusMessage.MessageId:
                                message = new ParameterStatusMessage();
                                break;
                            case ParseCompleteMessage.MessageId:
                                message = new ParseCompleteMessage();
                                break;
                            case PortalSuspendedMessage.MessageId:
                                message = new PortalSuspendedMessage();
                                break;
                            case ReadyForQueryMessage.MessageId:
                                message = new ReadyForQueryMessage();
                                break;
                            case RowDescriptionMessage.MessageId:
                                message = new RowDescriptionMessage();
                                break;
                            default:
                                // TODO: Are we supposed to fastforward instead?
                                throw new PostgresInvalidMessageId(
                                    state.TypeId);
                        }

                        try
                        {
                            message.Read(ref clientState,
                                new BinaryBuffer(state.Data, 0),
                                state.Length);
                        }
                        catch
                        {
                            if (message is IDisposable disposableMessage)
                            {
                                disposableMessage.Dispose();
                            }

                            throw;
                        }

                        state.Reset();
                        return true;
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
        Start, Type, Length, Data, Decode
    }

    internal struct PostgresReadState
    {
        public PostgresReadStatePosition Position;
        public byte TypeId;
        public int Length;
        public byte[] Data;
        public int TryReadState;
        public int DataLeft;

        public void Reset()
        {
            Position = PostgresReadStatePosition.Start;
            TypeId = 0;
            Length = 0;
            Data = null;
            TryReadState = 0;
            DataLeft = 0;
        }
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

    public enum MessageFieldType
    {
        /// <summary>the field contents are ERROR, FATAL, or PANIC (in an error message), or WARNING, NOTICE, DEBUG, INFO, or LOG (in a notice message), or a localized translation of one of these. Always present.</summary>
        Severity = (byte)'S',
        /// <summary>the SQLSTATE code for the error (see Appendix A). Not localizable. Always present.</summary>
        Code = (byte)'C',
        /// <summary>the primary human-readable error message. This should be accurate but terse (typically one line). Always present.</summary>
        Message = (byte)'M',
        /// <summary>an optional secondary error message carrying more detail about the problem. Might run to multiple lines.</summary>
        Detail = (byte)'D',
        /// <summary>an optional suggestion what to do about the problem. This is intended to differ from Detail in that it offers advice (potentially inappropriate) rather than hard facts. Might run to multiple lines.</summary>
        Hint = (byte)'H',
        /// <summary>the field value is a decimal ASCII integer, indicating an error cursor position as an index into the original query string. The first character has index 1, and positions are measured in characters not bytes.</summary>
        Position = (byte)'P',
        /// <summary>this is defined the same as the P field, but it is used when the cursor position refers to an internally generated command rather than the one submitted by the client. The q field will always appear when this field appears.</summary>
        InternalPosition = (byte)'p',
        /// <summary>the text of a failed internally-generated command. This could be, for example, a SQL query issued by a PL/pgSQL function.</summary>
        InternalQuery = (byte)'q',
        /// <summary>an indication of the context in which the error occurred. Presently this includes a call stack traceback of active procedural language functions and internally-generated queries. The trace is one entry per line, most recent first.</summary>
        Where = (byte)'W',
        /// <summary>if the error was associated with a specific database object, the name of the schema containing that object, if any.</summary>
        SchemaName = (byte)'s',
        /// <summary>if the error was associated with a specific table, the name of the table. (Refer to the schema name field for the name of the table's schema.)</summary>
        TableName = (byte)'t',
        /// <summary>if the error was associated with a specific table column, the name of the column. (Refer to the schema and table name fields to identify the table.)</summary>
        ColumnName = (byte)'c',
        /// <summary>if the error was associated with a specific data type, the name of the data type. (Refer to the schema name field for the name of the data type's schema.)</summary>
        DataTypeName = (byte)'d',
        /// <summary>if the error was associated with a specific constraint, the name of the constraint. Refer to fields listed above for the associated table or domain. (For this purpose, indexes are treated as constraints, even if they weren't created with constraint syntax.)</summary>
        ConstraintName = (byte)'n',
        /// <summary>the file name of the source-code location where the error was reported.</summary>
        File = (byte)'F',
        /// <summary>the line number of the source-code location where the error was reported.</summary>
        Line = (byte)'L',
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

            AssertMessageValue.Positive(
                nameof(message.ColumnCount),
                message.ColumnCount);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Positive(string name, int actual)
        {
            if (actual < 0)
            {
                throw new ArgumentOutOfRangeException(
                    name, actual, "Value must be positive.");
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
            _ms.Position = _lengthPos - 4;
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

    public class PostgresInvalidMessageId : Exception
    {
        public PostgresInvalidMessageId(byte typeId)
            : base($"Invalid type id 0x{typeId:X2} ('{(char)typeId}') given by server.")
        {
        }
    }

    internal struct PostgresClientState
    {
        public Encoding ClientEncoding;
        public Encoding ServerEncoding;
        public bool IntegerDatetimes;

        private static bool ReadBoolean(string boolean)
        {
            switch (boolean)
            {
                case "on": return true;
                case "off": return false;
            }

            throw new ArgumentOutOfRangeException(
                nameof(boolean), boolean,
                "Unexpected value. Must be 'on' or 'off'.");
        }

        public void SetParameter(ParameterStatusMessage parameter)
        {
            switch (parameter.ParameterName)
            {
                case PostgresProperties.IntegerDatetimes:
                    IntegerDatetimes = ReadBoolean(parameter.Value);
                    break;
            }
        }

        public static PostgresClientState CreateDefault()
        {
            return new PostgresClientState {
                ClientEncoding = Encoding.ASCII,
                ServerEncoding = Encoding.ASCII,
                IntegerDatetimes = true
            };
        }
    }

    internal struct AuthenticationMessage : IPostgresMessage, IDisposable
    {
        public const byte MessageId = (byte) 'R';

        public AuthenticationMessageType AuthenticationMessageType { get; internal set; }

        // AuthenticationMessageType.MD5Password type only.
        public byte[] MD5PasswordSalt => _dataBuffer;

        // AuthenticationMessageType.GSSContinue type only.
        public byte[] GSSAuthenticationData => _dataBuffer;

        // For testing purposes.
        internal byte[] DataBuffer
        {
            get => _dataBuffer;
            set => _dataBuffer = value;
        }

        private byte[] _dataBuffer;

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            AuthenticationMessageType = (AuthenticationMessageType)bb
                .ReadIntNetwork();

            switch (AuthenticationMessageType)
            {
                case AuthenticationMessageType.MD5Password:
                    AssertMessageValue.Length(12, length);
                    _dataBuffer = PostgresMessage.ReadByteArray(bb, 4);
                    break;
                case AuthenticationMessageType.GSSContinue:
                    _dataBuffer = PostgresMessage
                        .ReadByteArray(bb, length - 8);
                    break;
                default:
                    AssertMessageValue.Length(8, length);
                    break;
            }
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            throw new PostgresServerOnlyMessageException();
        }

        public void AsssertIsOk()
        {
            if (AuthenticationMessageType != AuthenticationMessageType.Ok)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(AuthenticationMessageType),
                    AuthenticationMessageType,
                    "Authentication error.");
            }
        }

        public void Dispose()
        {
            ArrayPool.Free(ref _dataBuffer);
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

    internal struct BindParameter
    {
        public int ParameterByteCount { get; set; }
        public byte[] Parameters { get; set; }

        public int Write(ref PostgresClientState state, MemoryStream ms)
        {
            AssertMessageValue.Positive(
                nameof(ParameterByteCount),
                ParameterByteCount);

            ms.WriteNetwork(ParameterByteCount);
            return ms.WriteNetwork(Parameters, ParameterByteCount) + 2;
        }
    }

    internal struct BindMessage : IPostgresMessage
    {
        public const byte MessageId = (byte)'B';

        public string PortalName { get; set; }
        public string PreparedStatementName { get; set; }
        public short FormatCodeCount { get; set; }
        public PostgresFormatCode[] FormatCodes { get; set; } // short[]
        public short ParameterCount { get; set; }
        public BindParameter[] Parameters { get; set; }
        public short ResultColumnFormatCodeCount { get; set; }
        public PostgresFormatCode[] ResultColumnFormatCodes { get; set; } // short[]

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            throw new PostgresClientOnlyMessageException();
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            AssertMessageValue.Positive(
                nameof(FormatCodeCount),
                FormatCodeCount);

            AssertMessageValue.Positive(
                nameof(ResultColumnFormatCodeCount),
                ResultColumnFormatCodeCount);

            ms.WriteByte(MessageId);

            var length = 4 + 2 + 2 + 2;
            var lengthPos = LengthPlacehold.Start(ms);
            length += ms.WriteString(PortalName, state.ClientEncoding);
            length += ms.WriteString(PreparedStatementName, state.ClientEncoding);
            ms.WriteNetwork(FormatCodeCount);
            for (var i = 0; i < FormatCodeCount; ++i)
            {
                ms.WriteNetwork((short)FormatCodes[i]);
                length += 2;
            }
            ms.WriteNetwork(ParameterCount);

            for (var i = 0; i < ParameterCount; ++i)
            {
                length += Parameters[i].Write(ref state, ms);
            }

            ms.WriteNetwork(ResultColumnFormatCodeCount);
            for (var i = 0; i < ResultColumnFormatCodeCount; ++i)
            {
                ms.WriteNetwork((short)ResultColumnFormatCodes[i]);
                length += 2;
            }

            lengthPos.WriteLength(length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteMessage(
            string portalName,
            string preparedStatementName,
            short formatCodeCount,
            PostgresFormatCode[] formatCodes,
            short parameterCount,
            BindParameter[] parameters,
            short resultColumnFormatCodeCount,
            PostgresFormatCode[] resultColumnFormatCodes,
            ref PostgresClientState state, MemoryStream ms)
        {
            var message = new BindMessage {
                PortalName = portalName,
                PreparedStatementName = preparedStatementName,
                FormatCodeCount = formatCodeCount,
                FormatCodes = formatCodes,
                ParameterCount = parameterCount,
                Parameters = parameters,
                ResultColumnFormatCodeCount = resultColumnFormatCodeCount,
                ResultColumnFormatCodes = resultColumnFormatCodes
            };

            message.Write(ref state, ms);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteMessage(
            int processId, int secretKey,
            ref PostgresClientState state, MemoryStream ms)
        {
            var message = new CancelRequestMessage {
                ProcessId = processId,
                SecretKey = secretKey
            };

            message.Write(ref state, ms);
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
            var lengthPos = LengthPlacehold.Start(ms);
            ms.WriteByte((byte)StatementTargetType);
            length += ms.WriteString(TargetName, state.ClientEncoding);

            lengthPos.WriteLength(length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteMessage(
            StatementTargetType statementTargetType, string targetName,
            ref PostgresClientState state, MemoryStream ms)
        {
            var message = new CloseMessage {
                StatementTargetType = statementTargetType,
                TargetName = targetName
            };

            message.Write(ref state, ms);
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
            AssertMessageValue.Positive(nameof(DataByteCount), DataByteCount);

            ms.WriteByte(MessageId);
            ms.WriteNetwork(DataByteCount + 4);
            ms.WriteNetwork(Data, DataByteCount);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteMessage(
            int dataByteCount, byte[] data,
            ref PostgresClientState state, MemoryStream ms)
        {
            var message = new CopyDataMessage {
                DataByteCount = dataByteCount,
                Data = data
            };

            message.Write(ref state, ms);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteMessage(
            ref PostgresClientState state, MemoryStream ms)
        {
            var message = new CopyDoneMessage();

            message.Write(ref state, ms);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteMessage(
            string errorMessage,
            ref PostgresClientState state, MemoryStream ms)
        {
            var message = new CopyFailMessage {
                ErrorMessage = errorMessage
            };

            message.Write(ref state, ms);
        }
    }

    internal struct CopyInResponseMessage : ICopyResponseMessage
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

    internal struct CopyOutResponseMessage : ICopyResponseMessage
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

    internal struct CopyBothResponseMessage : ICopyResponseMessage
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

    internal struct DataRow : IDisposable
    {
        public int Length { get; internal set; }
        public byte[] Data => _data;
        public bool IsNull { get; internal set; }

        private byte[] _data;
        private bool _pooledArray;

        internal DataRow(int length, byte[] data)
        {
            Length = length;
            IsNull = data == null;
            _data = data;
            _pooledArray = false;
        }

        public static DataRow Create(ref BinaryBuffer bb)
        {
            var row = new DataRow();

            try
            {
                row.Length = bb.ReadIntNetwork();

                switch (row.Length)
                {
                    case -1:
                        row.Length = 0;
                        row.IsNull = true;
                        row._data = EmptyArray<byte>.Value;
                        break;
                    case 0:
                        row._data = EmptyArray<byte>.Value;
                        break;
                    default:
                        AssertMessageValue.Positive(
                            nameof(row.Length), row.Length);

                        row._data = ArrayPool<byte>.GetArray(row.Length);
                        row._pooledArray = true;
                        bb.CopyTo(row._data, 0, row.Length);
                        break;
                }
            }
            catch
            {
                row.Dispose();
                throw;
            }

            return row;
        }

        public void Dispose()
        {
            if (_pooledArray)
            {
                ArrayPool.Free(ref _data);
            }
        }
    }

    internal struct DataRowMessage : IPostgresMessage, IDisposable
    {
        public const byte MessageId = (byte)'D';

        public short ColumnCount { get; private set; }
        public DataRow[] Rows => _rows;

        private DataRow[] _rows;

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            ColumnCount = bb.ReadShortNetwork();
            AssertMessageValue.Positive(nameof(ColumnCount), ColumnCount);

            var actualLength = 6;

            if (ColumnCount == 0)
            {
                _rows = EmptyArray<DataRow>.Value;
            }
            else
            {
                _rows = ArrayPool<DataRow>.GetArray(ColumnCount);

                for (var i = 0; i < ColumnCount; ++i)
                {
                    var row = DataRow.Create(ref bb);
                    _rows[i] = row;

                    actualLength += row.Length + 4;
                }
            }

            AssertMessageValue.Length(actualLength, length);
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            throw new PostgresServerOnlyMessageException();
        }

        public void Dispose()
        {
            var rows = Interlocked.Exchange(ref _rows, null);

            if (rows != null)
            {
                for (var i = 0; i < rows.Length; ++i)
                {
                    rows[i].Dispose();
                }

                ArrayPool.Free(ref rows);
            }
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteMessage(
            StatementTargetType statementTargetType, string targetName,
            ref PostgresClientState state, MemoryStream ms)
        {
            var message = new DescribeMessage {
                StatementTargetType = statementTargetType,
                TargetName = targetName
            };

            message.Write(ref state, ms);
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

    public struct FieldValueResponse
    {
        private static List<FieldValueResponse> _responseList;

        public MessageFieldType FieldType { get; private set; } // byte
        public string Value { get; private set; }

        internal int ComputedLength { get; private set; }

        internal static FieldValueResponse? Create(
            ref PostgresClientState state, ref BinaryBuffer bb)
        {
            var type = bb.ReadByte();

            if (type == 0)
            {
                return null;
            }

            return new FieldValueResponse {
                FieldType = (MessageFieldType)type,
                Value = bb.ReadString(state.ServerEncoding, out var sLength),
                ComputedLength = 1 + sLength
            };
        }

        internal static FieldValueResponse[] CreateAll(
            ref PostgresClientState state, BinaryBuffer bb,
            out int length, out int count)
        {
            var responseList = Interlocked.Exchange(ref _responseList, null)
                ?? new List<FieldValueResponse>();

            length = 0;
            count = 0;

            while (true)
            {
                var error = Create(ref state, ref bb);

                if (!error.HasValue)
                {
                    length += 1; // Count the terminating '\0'
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteMessage(
            string portalName, int rowCountResultMax,
            ref PostgresClientState state, MemoryStream ms)
        {
            var message = new ExecuteMessage {
                PortalName = portalName,
                RowCountResultMax = rowCountResultMax
            };

            message.Write(ref state, ms);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteMessage(
            ref PostgresClientState state, MemoryStream ms)
        {
            var message = new FlushMessage();

            message.Write(ref state, ms);
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
            AssertMessageValue.Positive(
                nameof(ArgumentFormatCodeCount),
                ArgumentFormatCodeCount);
            AssertMessageValue.Positive(
                nameof(ArgumentCount),
                ArgumentCount);
            AssertMessageValue.Positive(
                nameof(ArgumentByteCount),
                ArgumentByteCount);

            ms.WriteByte(MessageId);
            ms.WriteNetwork(18 + ArgumentFormatCodeCount * 2 + ArgumentByteCount);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteMessage(
            int objectId,
            short argumentFormatCodeCount,
            PostgresFormatCode[] argumentFormatCodes,
            short argumentCount,
            int argumentByteCount,
            byte[] argumentData,
            PostgresFormatCode resultFormatCode,
            ref PostgresClientState state, MemoryStream ms)
        {
            var message = new FunctionCallMessage {
                ObjectId = objectId,
                ArgumentFormatCodeCount = argumentFormatCodeCount,
                ArgumentFormatCodes = argumentFormatCodes,
                ArgumentCount = argumentCount,
                ArgumentByteCount= argumentByteCount,
                ArgumentData = argumentData,
                ResultFormatCode = resultFormatCode
            };

            message.Write(ref state, ms);
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
            AssertMessageValue.Positive(
                nameof(ResponseByteCount),
                ResponseByteCount);

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

    internal struct NoticeResponseMessage : IPostgresMessage, IDisposable
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

        // Used to create a copy that is not managed by our object pool and is
        // of the exact size.
        public FieldValueResponse[] PublicCloneNotices()
        {
            if (NoticeCount == 0)
            {
                return EmptyArray<FieldValueResponse>.Value;
            }

            var clone = new FieldValueResponse[NoticeCount];

            for (var i = 0; i < NoticeCount; ++i)
            {
                clone[i] = _notices[i];
            }

            return clone;
        }

        public void Dispose()
        {
            ArrayPool.Free(ref _notices);
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
            AssertMessageValue.Positive(
                nameof(ParameterTypeCount),
                ParameterTypeCount);

            ms.WriteByte(MessageId);

            var length = 6;
            var lengthPos = LengthPlacehold.Start(ms);
            length += ms.WriteString(DestinationName, state.ClientEncoding);
            length += ms.WriteString(Query, state.ClientEncoding);
            ms.WriteNetwork(ParameterTypeCount);
            length += ms.WriteNetwork(ObjectId, ParameterTypeCount);

            lengthPos.WriteLength(length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteMessage(
            string destinationName,
            string query,
            short parameterTypeCount,
            int[] objectId,
            ref PostgresClientState state, MemoryStream ms)
        {
            var message = new ParseMessage {
                DestinationName = destinationName,
                Query = query,
                ParameterTypeCount = parameterTypeCount,
                ObjectId = objectId,
            };

            message.Write(ref state, ms);
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

    internal struct PasswordMessage : IPostgresMessage, IDisposable
    {
        public const byte MessageId = (byte)'p';

        public int PasswordLength { get; set; }
        public byte[] Password
        {
            get => _password;
            set => _password = value;
        }

        private byte[] _password;
        private readonly bool _pooledArray;

        public PasswordMessage(bool pooledArray)
        {
            PasswordLength = 0;
            _password = null;
            _pooledArray = pooledArray;
        }

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            throw new PostgresClientOnlyMessageException();
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            Argument.HasValue(nameof(Password), Password);

            ms.WriteByte(MessageId);
            // Length + terminator.
            ms.WriteNetwork(4 + 1 + PasswordLength);
            ms.Write(Password, 0, PasswordLength);
            ms.WriteByte(0);
        }

        public void Dispose()
        {
            if (_pooledArray)
            {
                ArrayPool.Free(ref _password);
            }
        }

        public static PasswordMessage CreateMd5(
            AuthenticationMessage authenticationMessage,
            PostgresClientState state,
            PostgresConnectionString connectionString)
        {
            // http://kn0.ninja/blog/postgresql-pass-the-hashed-hash/
            // MD5(MD5(P + username) + connection-salt))

            const int saltLength = 4;
            var salt = authenticationMessage.MD5PasswordSalt;

            Argument.HasValue(nameof(salt), salt);
            Argument.IsEqual(nameof(salt), saltLength, salt.Length);

            var passwordMessage = new PasswordMessage();
            MD5 md5 = null;
            byte[] buffer = null;
            byte[] asciiBuffer = null;
            var encoding = state.ClientEncoding;
            var username = connectionString.Username;
            var password = connectionString.Password;

            try
            {
                md5 = MD5.Create();
                var characterCount = username.Length + password.Length;
                var maxByteCount = encoding.GetMaxByteCount(characterCount);
                buffer = ArrayPool<byte>.GetArray(maxByteCount);
                var actualByteCount = encoding.GetBytes(
                    password, 0, password.Length, buffer, 0);
                actualByteCount += encoding.GetBytes(
                    username, 0, username.Length, buffer, actualByteCount);

                var unsaltedHash = md5.ComputeHash(buffer, 0, actualByteCount);
                var saltedHashLength = unsaltedHash.Length * 2 + saltLength;

                ArrayPool.Free(ref buffer);
                buffer = ArrayPool<byte>.GetArray(saltedHashLength);

                HexEncoding.WriteAscii(
                    buffer, 0, unsaltedHash.Length,
                    unsaltedHash, 0, unsaltedHash.Length);

                buffer[saltedHashLength - saltLength] = salt[0];
                buffer[saltedHashLength - saltLength + 1] = salt[1];
                buffer[saltedHashLength - saltLength + 2] = salt[2];
                buffer[saltedHashLength - saltLength + 3] = salt[3];

                var saltedHash = md5.ComputeHash(buffer, 0, saltedHashLength);

                // + 3 is for the "md5" prefix.
                var passwordLength = saltedHash.Length * 2 + 3;

                asciiBuffer = ArrayPool<byte>.GetArray(passwordLength);

                HexEncoding.WriteAscii(
                    asciiBuffer, 3, passwordLength,
                    saltedHash, 0, saltedHash.Length);

                asciiBuffer[0] = (byte)'m';
                asciiBuffer[1] = (byte)'d';
                asciiBuffer[2] = (byte)'5';

                passwordMessage.Password = asciiBuffer;
                passwordMessage.PasswordLength = passwordLength;
            }
            catch
            {
                // Only free in case of exception. In the non-exceptional case
                // this will be free'ed when PasswordMessage is disposed.
                ArrayPool.Free(ref asciiBuffer);
            }
            finally
            {
                ArrayPool.Free(ref buffer);
                md5.TryDispose();
            }

            return passwordMessage;
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


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteMessage(
            string query,
            ref PostgresClientState state, MemoryStream ms)
        {
            var message = new QueryMessage {
                Query = query
            };

            message.Write(ref state, ms);
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

        public void AssertType(TransactionIndicatorType type)
        {
            if (TransactionIndicatorType != type)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(TransactionIndicatorType),
                    TransactionIndicatorType,
                    $"Unexpected transaction type, expected {type}.");
            }
        }
    }

    internal struct ColumnDescription
    {
        public string Name { get; internal set; }
        public int TableObjectId { get; internal set; }
        public short ColumnIndex { get; internal set; }
        public int DataTypeObjectId { get; internal set; }
        public short DataTypeSize { get; internal set; }
        public int TypeModifier { get; internal set; }
        public PostgresFormatCode FormatCode { get; internal set; } // short

        // Computed by us.
        public int ComputedLength { get; private set; }

        internal static ColumnDescription Create(
            ref PostgresClientState state, ref BinaryBuffer bb)
        {
            var description = new ColumnDescription {
                Name = bb.ReadString(state.ServerEncoding, out var sLength),
                TableObjectId = bb.ReadIntNetwork(),
                ColumnIndex = bb.ReadShortNetwork(),
                DataTypeObjectId = bb.ReadIntNetwork(),
                DataTypeSize = bb.ReadShortNetwork(),
                TypeModifier = bb.ReadIntNetwork(),
                FormatCode = (PostgresFormatCode)bb.ReadShortNetwork(),
                ComputedLength = sLength + 18
            };

            // ColumnIndex can be negative.
            // TODO: Find out when/why.

            // DataTypeSize can be negative.
            // "Note that negative values denote variable-width types."

            return description;
        }
    }

    internal struct RowDescriptionMessage : IPostgresMessage, IDisposable
    {
        public const byte MessageId = (byte)'T';

        public short FieldCount { get; private set; }
        public ColumnDescription[] Fields => _fields;

        private ColumnDescription[] _fields;

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            FieldCount = bb.ReadShortNetwork();
            AssertMessageValue.Positive(nameof(FieldCount), FieldCount);

            _fields = ArrayPool<ColumnDescription>.GetArray(FieldCount);
            var actualLength = 6;
            for (var i = 0; i < FieldCount; ++i)
            {
                var field = ColumnDescription.Create(ref state, ref bb);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteMessage(
            ref PostgresClientState state, MemoryStream ms)
        {
            var message = new SSLRequestMessage();

            message.Write(ref state, ms);
        }
    }

    internal struct StartupMessage : IPostgresMessage
    {
        public int MessageCount { get; set; }
        public KeyValuePair<string, string>[] Messages { get; set; }

        public void Read(ref PostgresClientState state, BinaryBuffer bb, int length)
        {
            throw new PostgresClientOnlyMessageException();
        }

        public void Write(ref PostgresClientState state, MemoryStream ms)
        {
            AssertMessageValue.Positive(
                nameof(MessageCount),
                MessageCount);

            // No message id sent.
            var length = 9;
            var lengthPos = LengthPlacehold.Start(ms);

            // Protocol version 3.0
            ms.WriteNetwork((int)0x0003_0000);

            var messages = Messages;

            if (messages != null)
            {
                for (var i = 0; i < MessageCount; ++i)
                {
                    var message = messages[i];

                    length += ms.WriteString(
                        message.Key, state.ClientEncoding);

                    length += ms.WriteString(
                        message.Value, state.ClientEncoding);
                }
            }

            ms.WriteByte(0);

            lengthPos.WriteLength(length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteMessage(
            ref PostgresClientState state, MemoryStream ms,
            int messageCount, KeyValuePair<string, string>[] messages)
        {
            var message = new StartupMessage {
                MessageCount = messageCount,
                Messages = messages
            };

            message.Write(ref state, ms);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteMessage(
            ref PostgresClientState state, MemoryStream ms)
        {
            var message = new SyncMessage();

            message.Write(ref state, ms);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteMessage(
            ref PostgresClientState state, MemoryStream ms)
        {
            var message = new TerminateMessage();

            message.Write(ref state, ms);
        }
    }
}