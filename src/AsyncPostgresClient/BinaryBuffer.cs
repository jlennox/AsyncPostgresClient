using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

#pragma warning disable CS3002 // Return type is not CLS-compliant
namespace AsyncPostgresClient
{
    public unsafe struct BinaryBuffer
    {
        private readonly byte[] _buffer;
        private int _offset;

        public byte[] Buffer => _buffer;

        public int Offset
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _offset;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                OffsetCheck(value);
                _offset = value;
            }
        }

        public BinaryBuffer(byte[] buffer, int offset)
            : this()
        {
            if (buffer == null) { throw new ArgumentNullException(nameof(buffer)); }
            if (offset < 0) { throw new ArgumentOutOfRangeException(nameof(offset), offset, "Value must be positive."); }
            if (offset > buffer.Length) { throw new ArgumentException("Index out of range.", nameof(buffer)); }

            _buffer = buffer;
            _offset = offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OffsetCheck(int offset)
        {
            if (offset < 0 || offset >= _buffer.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(offset), offset,
                    "Cannot seek before start of or beyond end of buffer.");
            }
        }

        public void Seek(int offset, SeekOrigin origin)
        {
            int newOffset;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    newOffset = offset;
                    break;
                case SeekOrigin.Current:
                    newOffset = _offset + offset;
                    break;
                case SeekOrigin.End:
                    newOffset = _buffer.Length - 1 + offset;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(origin), origin, null);
            }

            OffsetCheck(newOffset);

            _offset = newOffset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryReadByte(
            byte[] buffer,
            ref int offset,
            ref int length,
            ref byte result)
        {
            if (length >= 1)
            {
                result = buffer[offset];
                ++offset;
                --length;
                return true;
            }

            return false;
        }

        public static bool TryReadInt(
            byte[] buffer,
            ref int offset,
            ref int length,
            ref int result,
            ref int readState)
        {
            if (length >= 4 && readState == 0)
            {
                result = ReadInt(buffer, offset);
                offset += 4;
                length -= 4;
                return true;
            }

            var shiftAmount = readState * 8;
            var i = 0;

            for (; i < length && readState < 4; ++i)
            {
                switch (readState)
                {
                    case 0:
                        result |= buffer[offset + i];
                        break;
                    case 1:
                        result |= buffer[offset + i] << 8;
                        break;
                    case 2:
                        result |= buffer[offset + i] << 16;
                        break;
                    case 3:
                        result |= buffer[offset + i] << 24;
                        break;
                }
                ++readState;
            }

            offset += i;
            length -= i;

            if (readState == 4)
            {
                readState = 0;
                return true;
            }

            return false;
        }

        public static bool TryReadIntNetwork(
            byte[] buffer,
            ref int offset,
            ref int length,
            ref int result,
            ref int readState)
        {
            if (length >= 4 && readState == 0)
            {
                result = ReadIntNetwork(buffer, offset);
                offset += 4;
                length -= 4;
                return true;
            }

            var i = 0;

            for (; i < length && readState < 4; ++i)
            {
                switch (readState)
                {
                    case 0:
                        result |= buffer[offset + i] << 24;
                        break;
                    case 1:
                        result |= buffer[offset + i] << 16;
                        break;
                    case 2:
                        result |= buffer[offset + i] << 8;
                        break;
                    case 3:
                        result |= buffer[offset + i];
                        break;
                }

                ++readState;
            }

            offset += i;
            length -= i;

            if (readState == 4)
            {
                readState = 0;
                return true;
            }

            return false;
        }

        public string ReadString(Encoding encoding, out int length)
        {
            if (encoding == null) { throw new ArgumentNullException(nameof(encoding)); }

            var start = _offset;
            var end = _offset;

            if (_buffer[_offset] == 0)
            {
                length = 1;
                return "";
            }

            // The first byte was checked on the empty string short curcuit.
            ++_offset;

            // TODO: This wont work for UTF16 and likely other encodings.
            for (; _offset < _buffer.Length; ++_offset)
            {
                if (_buffer[_offset] == 0)
                {
                    end = _offset;
                    ++_offset;
                    break;
                }
            }

            var strLength = end - start;

            length = strLength + 1;

            return encoding.GetString(_buffer, start, strLength);
        }

        public string ReadString(Encoding encoding)
        {
            return ReadString(encoding, out var _);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(
            byte[] destination, int destinationOffset, int length)
        {
            System.Buffer.BlockCopy(
                _buffer, _offset,
                destination, destinationOffset, length);

            _offset += length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] Copy(int length)
        {
            var destination = new byte[length];

            CopyTo(destination, 0, length);

            return destination;
        }

        public byte PeekByte()
        {
            return _buffer[_offset];
        }

        public byte ReadByte()
        {
            var result = _buffer[_offset];
            ++_offset;
            return result;
        }

        public void WriteByte(byte i)
        {
            _buffer[_offset] = i;
            ++_offset;
        }

        public short PeekShort()
        {
            if (_buffer.Length - _offset < 2) { throw new ArgumentException("Index out of range.", nameof(_buffer)); }

            fixed (byte* bufferPtr = &_buffer[_offset])
            {
                return (short)(((short)bufferPtr[0] << 0) |
                    ((short)bufferPtr[1] << 8));
            }
        }

        public short ReadShort()
        {
            var result = PeekShort();
            _offset += 2;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short ReadShort(
            byte[] buffer, int offset)
        {
            if (buffer == null) { throw new ArgumentNullException(nameof(buffer)); }
            if (offset < 0) { throw new ArgumentOutOfRangeException(nameof(offset), offset, "Value must be positive."); }
            if (buffer.Length - offset < 2) { throw new ArgumentException("Index out of range.", nameof(buffer)); }

            fixed (byte* bufferPtr = &buffer[offset])
            {
                return ReadShortUnsafe(bufferPtr);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short ReadShortUnsafe(
            byte[] buffer, int offset)
        {
            fixed (byte* bufferPtr = &buffer[offset])
            {
                return ReadShortUnsafe(bufferPtr);
            }
        }

        public static short ReadShortUnsafe(
            byte* bufferPtr)
        {
            return (short)(((short)bufferPtr[0] << 0) |
                ((short)bufferPtr[1] << 8));
        }

        public void WriteShort(short i)
        {
            if (_buffer.Length - _offset < 2) { throw new ArgumentException("Index out of range.", nameof(_buffer)); }

            WriteShortUnsafe(_buffer, _offset, i);
            _offset += 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteShort(
            byte[] buffer, int offset, short i)
        {
            if (buffer == null) { throw new ArgumentNullException(nameof(buffer)); }
            if (offset < 0) { throw new ArgumentOutOfRangeException(nameof(offset), offset, "Value must be positive."); }
            if (buffer.Length - offset < 2) { throw new ArgumentException("Index out of range.", nameof(buffer)); }

            fixed (byte* bufferPtr = &buffer[offset])
            {
                WriteShortUnsafe(bufferPtr, i);
            }
        }

        public static void WriteShortUnsafe(
            byte[] buffer, int offset, short i)
        {
            fixed (byte* bufferPtr = &buffer[offset])
            {
                WriteShortUnsafe(bufferPtr, i);
            }
        }

        public static void WriteShortUnsafe(
            byte* bufferPtr, short i)
        {
            bufferPtr[0] = (byte)((i >> 0) & 0xFF);
            bufferPtr[1] = (byte)((i >> 8) & 0xFF);
        }

        public short PeekShortNetwork()
        {
            if (_buffer.Length - _offset < 2) { throw new ArgumentException("Index out of range.", nameof(_buffer)); }

            fixed (byte* bufferPtr = &_buffer[_offset])
            {
                return (short)(((short)bufferPtr[0] << 8) |
                    ((short)bufferPtr[1] << 0));
            }
        }

        public short ReadShortNetwork()
        {
            var result = PeekShortNetwork();
            _offset += 2;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short ReadShortNetwork(
            byte[] buffer, int offset)
        {
            if (buffer == null) { throw new ArgumentNullException(nameof(buffer)); }
            if (offset < 0) { throw new ArgumentOutOfRangeException(nameof(offset), offset, "Value must be positive."); }
            if (buffer.Length - offset < 2) { throw new ArgumentException("Index out of range.", nameof(buffer)); }

            fixed (byte* bufferPtr = &buffer[offset])
            {
                return ReadShortNetworkUnsafe(bufferPtr);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short ReadShortNetworkUnsafe(
            byte[] buffer, int offset)
        {
            fixed (byte* bufferPtr = &buffer[offset])
            {
                return ReadShortNetworkUnsafe(bufferPtr);
            }
        }

        public static short ReadShortNetworkUnsafe(
            byte* bufferPtr)
        {
            return (short)(((short)bufferPtr[0] << 8) |
                ((short)bufferPtr[1] << 0));
        }

        public void WriteShortNetwork(short i)
        {
            if (_buffer.Length - _offset < 2) { throw new ArgumentException("Index out of range.", nameof(_buffer)); }

            WriteShortNetworkUnsafe(_buffer, _offset, i);
            _offset += 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteShortNetwork(
            byte[] buffer, int offset, short i)
        {
            if (buffer == null) { throw new ArgumentNullException(nameof(buffer)); }
            if (offset < 0) { throw new ArgumentOutOfRangeException(nameof(offset), offset, "Value must be positive."); }
            if (buffer.Length - offset < 2) { throw new ArgumentException("Index out of range.", nameof(buffer)); }

            fixed (byte* bufferPtr = &buffer[offset])
            {
                WriteShortNetworkUnsafe(bufferPtr, i);
            }
        }

        public static void WriteShortNetworkUnsafe(
            byte[] buffer, int offset, short i)
        {
            fixed (byte* bufferPtr = &buffer[offset])
            {
                WriteShortNetworkUnsafe(bufferPtr, i);
            }
        }

        public static void WriteShortNetworkUnsafe(
            byte* bufferPtr, short i)
        {
            bufferPtr[0] = (byte)((i >> 8) & 0xFF);
            bufferPtr[1] = (byte)((i >> 0) & 0xFF);
        }

        public ushort PeekUShort()
        {
            if (_buffer.Length - _offset < 2) { throw new ArgumentException("Index out of range.", nameof(_buffer)); }

            fixed (byte* bufferPtr = &_buffer[_offset])
            {
                return (ushort)(((ushort)bufferPtr[0] << 0) |
                    ((ushort)bufferPtr[1] << 8));
            }
        }

        public ushort ReadUShort()
        {
            var result = PeekUShort();
            _offset += 2;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReadUShort(
            byte[] buffer, int offset)
        {
            if (buffer == null) { throw new ArgumentNullException(nameof(buffer)); }
            if (offset < 0) { throw new ArgumentOutOfRangeException(nameof(offset), offset, "Value must be positive."); }
            if (buffer.Length - offset < 2) { throw new ArgumentException("Index out of range.", nameof(buffer)); }

            fixed (byte* bufferPtr = &buffer[offset])
            {
                return ReadUShortUnsafe(bufferPtr);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReadUShortUnsafe(
            byte[] buffer, int offset)
        {
            fixed (byte* bufferPtr = &buffer[offset])
            {
                return ReadUShortUnsafe(bufferPtr);
            }
        }

        public static ushort ReadUShortUnsafe(
            byte* bufferPtr)
        {
            return (ushort)(((ushort)bufferPtr[0] << 0) |
                ((ushort)bufferPtr[1] << 8));
        }

        public void WriteUShort(ushort i)
        {
            if (_buffer.Length - _offset < 2) { throw new ArgumentException("Index out of range.", nameof(_buffer)); }

            WriteUShortUnsafe(_buffer, _offset, i);
            _offset += 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUShort(
            byte[] buffer, int offset, ushort i)
        {
            if (buffer == null) { throw new ArgumentNullException(nameof(buffer)); }
            if (offset < 0) { throw new ArgumentOutOfRangeException(nameof(offset), offset, "Value must be positive."); }
            if (buffer.Length - offset < 2) { throw new ArgumentException("Index out of range.", nameof(buffer)); }

            fixed (byte* bufferPtr = &buffer[offset])
            {
                WriteUShortUnsafe(bufferPtr, i);
            }
        }

        public static void WriteUShortUnsafe(
            byte[] buffer, int offset, ushort i)
        {
            fixed (byte* bufferPtr = &buffer[offset])
            {
                WriteUShortUnsafe(bufferPtr, i);
            }
        }

        public static void WriteUShortUnsafe(
            byte* bufferPtr, ushort i)
        {
            bufferPtr[0] = (byte)((i >> 0) & 0xFF);
            bufferPtr[1] = (byte)((i >> 8) & 0xFF);
        }

        public ushort PeekUShortNetwork()
        {
            if (_buffer.Length - _offset < 2) { throw new ArgumentException("Index out of range.", nameof(_buffer)); }

            fixed (byte* bufferPtr = &_buffer[_offset])
            {
                return (ushort)(((ushort)bufferPtr[0] << 8) |
                    ((ushort)bufferPtr[1] << 0));
            }
        }

        public ushort ReadUShortNetwork()
        {
            var result = PeekUShortNetwork();
            _offset += 2;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReadUShortNetwork(
            byte[] buffer, int offset)
        {
            if (buffer == null) { throw new ArgumentNullException(nameof(buffer)); }
            if (offset < 0) { throw new ArgumentOutOfRangeException(nameof(offset), offset, "Value must be positive."); }
            if (buffer.Length - offset < 2) { throw new ArgumentException("Index out of range.", nameof(buffer)); }

            fixed (byte* bufferPtr = &buffer[offset])
            {
                return ReadUShortNetworkUnsafe(bufferPtr);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReadUShortNetworkUnsafe(
            byte[] buffer, int offset)
        {
            fixed (byte* bufferPtr = &buffer[offset])
            {
                return ReadUShortNetworkUnsafe(bufferPtr);
            }
        }

        public static ushort ReadUShortNetworkUnsafe(
            byte* bufferPtr)
        {
            return (ushort)(((ushort)bufferPtr[0] << 8) |
                ((ushort)bufferPtr[1] << 0));
        }

        public void WriteUShortNetwork(ushort i)
        {
            if (_buffer.Length - _offset < 2) { throw new ArgumentException("Index out of range.", nameof(_buffer)); }

            WriteUShortNetworkUnsafe(_buffer, _offset, i);
            _offset += 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUShortNetwork(
            byte[] buffer, int offset, ushort i)
        {
            if (buffer == null) { throw new ArgumentNullException(nameof(buffer)); }
            if (offset < 0) { throw new ArgumentOutOfRangeException(nameof(offset), offset, "Value must be positive."); }
            if (buffer.Length - offset < 2) { throw new ArgumentException("Index out of range.", nameof(buffer)); }

            fixed (byte* bufferPtr = &buffer[offset])
            {
                WriteUShortNetworkUnsafe(bufferPtr, i);
            }
        }

        public static void WriteUShortNetworkUnsafe(
            byte[] buffer, int offset, ushort i)
        {
            fixed (byte* bufferPtr = &buffer[offset])
            {
                WriteUShortNetworkUnsafe(bufferPtr, i);
            }
        }

        public static void WriteUShortNetworkUnsafe(
            byte* bufferPtr, ushort i)
        {
            bufferPtr[0] = (byte)((i >> 8) & 0xFF);
            bufferPtr[1] = (byte)((i >> 0) & 0xFF);
        }

        public int PeekInt()
        {
            if (_buffer.Length - _offset < 4) { throw new ArgumentException("Index out of range.", nameof(_buffer)); }

            fixed (byte* bufferPtr = &_buffer[_offset])
            {
                return ((int)bufferPtr[0] << 0) |
                    ((int)bufferPtr[1] << 8) |
                    ((int)bufferPtr[2] << 16) |
                    ((int)bufferPtr[3] << 24);
            }
        }

        public int ReadInt()
        {
            var result = PeekInt();
            _offset += 4;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadInt(
            byte[] buffer, int offset)
        {
            if (buffer == null) { throw new ArgumentNullException(nameof(buffer)); }
            if (offset < 0) { throw new ArgumentOutOfRangeException(nameof(offset), offset, "Value must be positive."); }
            if (buffer.Length - offset < 4) { throw new ArgumentException("Index out of range.", nameof(buffer)); }

            fixed (byte* bufferPtr = &buffer[offset])
            {
                return ReadIntUnsafe(bufferPtr);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadIntUnsafe(
            byte[] buffer, int offset)
        {
            fixed (byte* bufferPtr = &buffer[offset])
            {
                return ReadIntUnsafe(bufferPtr);
            }
        }

        public static int ReadIntUnsafe(
            byte* bufferPtr)
        {
            return ((int)bufferPtr[0] << 0) |
                ((int)bufferPtr[1] << 8) |
                ((int)bufferPtr[2] << 16) |
                ((int)bufferPtr[3] << 24);
        }

        public void WriteInt(int i)
        {
            if (_buffer.Length - _offset < 4) { throw new ArgumentException("Index out of range.", nameof(_buffer)); }

            WriteIntUnsafe(_buffer, _offset, i);
            _offset += 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteInt(
            byte[] buffer, int offset, int i)
        {
            if (buffer == null) { throw new ArgumentNullException(nameof(buffer)); }
            if (offset < 0) { throw new ArgumentOutOfRangeException(nameof(offset), offset, "Value must be positive."); }
            if (buffer.Length - offset < 4) { throw new ArgumentException("Index out of range.", nameof(buffer)); }

            fixed (byte* bufferPtr = &buffer[offset])
            {
                WriteIntUnsafe(bufferPtr, i);
            }
        }

        public static void WriteIntUnsafe(
            byte[] buffer, int offset, int i)
        {
            fixed (byte* bufferPtr = &buffer[offset])
            {
                WriteIntUnsafe(bufferPtr, i);
            }
        }

        public static void WriteIntUnsafe(
            byte* bufferPtr, int i)
        {
            bufferPtr[0] = (byte)((i >> 0) & 0xFF);
            bufferPtr[1] = (byte)((i >> 8) & 0xFF);
            bufferPtr[2] = (byte)((i >> 16) & 0xFF);
            bufferPtr[3] = (byte)((i >> 24) & 0xFF);
        }

        public int PeekIntNetwork()
        {
            if (_buffer.Length - _offset < 4) { throw new ArgumentException("Index out of range.", nameof(_buffer)); }

            fixed (byte* bufferPtr = &_buffer[_offset])
            {
                return ((int)bufferPtr[0] << 24) |
                    ((int)bufferPtr[1] << 16) |
                    ((int)bufferPtr[2] << 8) |
                    ((int)bufferPtr[3] << 0);
            }
        }

        public int ReadIntNetwork()
        {
            var result = PeekIntNetwork();
            _offset += 4;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadIntNetwork(
            byte[] buffer, int offset)
        {
            if (buffer == null) { throw new ArgumentNullException(nameof(buffer)); }
            if (offset < 0) { throw new ArgumentOutOfRangeException(nameof(offset), offset, "Value must be positive."); }
            if (buffer.Length - offset < 4) { throw new ArgumentException("Index out of range.", nameof(buffer)); }

            fixed (byte* bufferPtr = &buffer[offset])
            {
                return ReadIntNetworkUnsafe(bufferPtr);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadIntNetworkUnsafe(
            byte[] buffer, int offset)
        {
            fixed (byte* bufferPtr = &buffer[offset])
            {
                return ReadIntNetworkUnsafe(bufferPtr);
            }
        }

        public static int ReadIntNetworkUnsafe(
            byte* bufferPtr)
        {
            return ((int)bufferPtr[0] << 24) |
                ((int)bufferPtr[1] << 16) |
                ((int)bufferPtr[2] << 8) |
                ((int)bufferPtr[3] << 0);
        }

        public void WriteIntNetwork(int i)
        {
            if (_buffer.Length - _offset < 4) { throw new ArgumentException("Index out of range.", nameof(_buffer)); }

            WriteIntNetworkUnsafe(_buffer, _offset, i);
            _offset += 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteIntNetwork(
            byte[] buffer, int offset, int i)
        {
            if (buffer == null) { throw new ArgumentNullException(nameof(buffer)); }
            if (offset < 0) { throw new ArgumentOutOfRangeException(nameof(offset), offset, "Value must be positive."); }
            if (buffer.Length - offset < 4) { throw new ArgumentException("Index out of range.", nameof(buffer)); }

            fixed (byte* bufferPtr = &buffer[offset])
            {
                WriteIntNetworkUnsafe(bufferPtr, i);
            }
        }

        public static void WriteIntNetworkUnsafe(
            byte[] buffer, int offset, int i)
        {
            fixed (byte* bufferPtr = &buffer[offset])
            {
                WriteIntNetworkUnsafe(bufferPtr, i);
            }
        }

        public static void WriteIntNetworkUnsafe(
            byte* bufferPtr, int i)
        {
            bufferPtr[0] = (byte)((i >> 24) & 0xFF);
            bufferPtr[1] = (byte)((i >> 16) & 0xFF);
            bufferPtr[2] = (byte)((i >> 8) & 0xFF);
            bufferPtr[3] = (byte)((i >> 0) & 0xFF);
        }

        public uint PeekUInt()
        {
            if (_buffer.Length - _offset < 4) { throw new ArgumentException("Index out of range.", nameof(_buffer)); }

            fixed (byte* bufferPtr = &_buffer[_offset])
            {
                return ((uint)bufferPtr[0] << 0) |
                    ((uint)bufferPtr[1] << 8) |
                    ((uint)bufferPtr[2] << 16) |
                    ((uint)bufferPtr[3] << 24);
            }
        }

        public uint ReadUInt()
        {
            var result = PeekUInt();
            _offset += 4;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReadUInt(
            byte[] buffer, int offset)
        {
            if (buffer == null) { throw new ArgumentNullException(nameof(buffer)); }
            if (offset < 0) { throw new ArgumentOutOfRangeException(nameof(offset), offset, "Value must be positive."); }
            if (buffer.Length - offset < 4) { throw new ArgumentException("Index out of range.", nameof(buffer)); }

            fixed (byte* bufferPtr = &buffer[offset])
            {
                return ReadUIntUnsafe(bufferPtr);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReadUIntUnsafe(
            byte[] buffer, int offset)
        {
            fixed (byte* bufferPtr = &buffer[offset])
            {
                return ReadUIntUnsafe(bufferPtr);
            }
        }

        public static uint ReadUIntUnsafe(
            byte* bufferPtr)
        {
            return ((uint)bufferPtr[0] << 0) |
                ((uint)bufferPtr[1] << 8) |
                ((uint)bufferPtr[2] << 16) |
                ((uint)bufferPtr[3] << 24);
        }

        public void WriteUInt(uint i)
        {
            if (_buffer.Length - _offset < 4) { throw new ArgumentException("Index out of range.", nameof(_buffer)); }

            WriteUIntUnsafe(_buffer, _offset, i);
            _offset += 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUInt(
            byte[] buffer, int offset, uint i)
        {
            if (buffer == null) { throw new ArgumentNullException(nameof(buffer)); }
            if (offset < 0) { throw new ArgumentOutOfRangeException(nameof(offset), offset, "Value must be positive."); }
            if (buffer.Length - offset < 4) { throw new ArgumentException("Index out of range.", nameof(buffer)); }

            fixed (byte* bufferPtr = &buffer[offset])
            {
                WriteUIntUnsafe(bufferPtr, i);
            }
        }

        public static void WriteUIntUnsafe(
            byte[] buffer, int offset, uint i)
        {
            fixed (byte* bufferPtr = &buffer[offset])
            {
                WriteUIntUnsafe(bufferPtr, i);
            }
        }

        public static void WriteUIntUnsafe(
            byte* bufferPtr, uint i)
        {
            bufferPtr[0] = (byte)((i >> 0) & 0xFF);
            bufferPtr[1] = (byte)((i >> 8) & 0xFF);
            bufferPtr[2] = (byte)((i >> 16) & 0xFF);
            bufferPtr[3] = (byte)((i >> 24) & 0xFF);
        }

        public uint PeekUIntNetwork()
        {
            if (_buffer.Length - _offset < 4) { throw new ArgumentException("Index out of range.", nameof(_buffer)); }

            fixed (byte* bufferPtr = &_buffer[_offset])
            {
                return ((uint)bufferPtr[0] << 24) |
                    ((uint)bufferPtr[1] << 16) |
                    ((uint)bufferPtr[2] << 8) |
                    ((uint)bufferPtr[3] << 0);
            }
        }

        public uint ReadUIntNetwork()
        {
            var result = PeekUIntNetwork();
            _offset += 4;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReadUIntNetwork(
            byte[] buffer, int offset)
        {
            if (buffer == null) { throw new ArgumentNullException(nameof(buffer)); }
            if (offset < 0) { throw new ArgumentOutOfRangeException(nameof(offset), offset, "Value must be positive."); }
            if (buffer.Length - offset < 4) { throw new ArgumentException("Index out of range.", nameof(buffer)); }

            fixed (byte* bufferPtr = &buffer[offset])
            {
                return ReadUIntNetworkUnsafe(bufferPtr);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReadUIntNetworkUnsafe(
            byte[] buffer, int offset)
        {
            fixed (byte* bufferPtr = &buffer[offset])
            {
                return ReadUIntNetworkUnsafe(bufferPtr);
            }
        }

        public static uint ReadUIntNetworkUnsafe(
            byte* bufferPtr)
        {
            return ((uint)bufferPtr[0] << 24) |
                ((uint)bufferPtr[1] << 16) |
                ((uint)bufferPtr[2] << 8) |
                ((uint)bufferPtr[3] << 0);
        }

        public void WriteUIntNetwork(uint i)
        {
            if (_buffer.Length - _offset < 4) { throw new ArgumentException("Index out of range.", nameof(_buffer)); }

            WriteUIntNetworkUnsafe(_buffer, _offset, i);
            _offset += 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUIntNetwork(
            byte[] buffer, int offset, uint i)
        {
            if (buffer == null) { throw new ArgumentNullException(nameof(buffer)); }
            if (offset < 0) { throw new ArgumentOutOfRangeException(nameof(offset), offset, "Value must be positive."); }
            if (buffer.Length - offset < 4) { throw new ArgumentException("Index out of range.", nameof(buffer)); }

            fixed (byte* bufferPtr = &buffer[offset])
            {
                WriteUIntNetworkUnsafe(bufferPtr, i);
            }
        }

        public static void WriteUIntNetworkUnsafe(
            byte[] buffer, int offset, uint i)
        {
            fixed (byte* bufferPtr = &buffer[offset])
            {
                WriteUIntNetworkUnsafe(bufferPtr, i);
            }
        }

        public static void WriteUIntNetworkUnsafe(
            byte* bufferPtr, uint i)
        {
            bufferPtr[0] = (byte)((i >> 24) & 0xFF);
            bufferPtr[1] = (byte)((i >> 16) & 0xFF);
            bufferPtr[2] = (byte)((i >> 8) & 0xFF);
            bufferPtr[3] = (byte)((i >> 0) & 0xFF);
        }

        public long PeekLong()
        {
            if (_buffer.Length - _offset < 8) { throw new ArgumentException("Index out of range.", nameof(_buffer)); }

            fixed (byte* bufferPtr = &_buffer[_offset])
            {
                return ((long)bufferPtr[0] << 0) |
                    ((long)bufferPtr[1] << 8) |
                    ((long)bufferPtr[2] << 16) |
                    ((long)bufferPtr[3] << 24) |
                    ((long)bufferPtr[4] << 32) |
                    ((long)bufferPtr[5] << 40) |
                    ((long)bufferPtr[6] << 48) |
                    ((long)bufferPtr[7] << 56);
            }
        }

        public long ReadLong()
        {
            var result = PeekLong();
            _offset += 8;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ReadLong(
            byte[] buffer, int offset)
        {
            if (buffer == null) { throw new ArgumentNullException(nameof(buffer)); }
            if (offset < 0) { throw new ArgumentOutOfRangeException(nameof(offset), offset, "Value must be positive."); }
            if (buffer.Length - offset < 8) { throw new ArgumentException("Index out of range.", nameof(buffer)); }

            fixed (byte* bufferPtr = &buffer[offset])
            {
                return ReadLongUnsafe(bufferPtr);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ReadLongUnsafe(
            byte[] buffer, int offset)
        {
            fixed (byte* bufferPtr = &buffer[offset])
            {
                return ReadLongUnsafe(bufferPtr);
            }
        }

        public static long ReadLongUnsafe(
            byte* bufferPtr)
        {
            return ((long)bufferPtr[0] << 0) |
                ((long)bufferPtr[1] << 8) |
                ((long)bufferPtr[2] << 16) |
                ((long)bufferPtr[3] << 24) |
                ((long)bufferPtr[4] << 32) |
                ((long)bufferPtr[5] << 40) |
                ((long)bufferPtr[6] << 48) |
                ((long)bufferPtr[7] << 56);
        }

        public void WriteLong(long i)
        {
            if (_buffer.Length - _offset < 8) { throw new ArgumentException("Index out of range.", nameof(_buffer)); }

            WriteLongUnsafe(_buffer, _offset, i);
            _offset += 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteLong(
            byte[] buffer, int offset, long i)
        {
            if (buffer == null) { throw new ArgumentNullException(nameof(buffer)); }
            if (offset < 0) { throw new ArgumentOutOfRangeException(nameof(offset), offset, "Value must be positive."); }
            if (buffer.Length - offset < 8) { throw new ArgumentException("Index out of range.", nameof(buffer)); }

            fixed (byte* bufferPtr = &buffer[offset])
            {
                WriteLongUnsafe(bufferPtr, i);
            }
        }

        public static void WriteLongUnsafe(
            byte[] buffer, int offset, long i)
        {
            fixed (byte* bufferPtr = &buffer[offset])
            {
                WriteLongUnsafe(bufferPtr, i);
            }
        }

        public static void WriteLongUnsafe(
            byte* bufferPtr, long i)
        {
            bufferPtr[0] = (byte)((i >> 0) & 0xFF);
            bufferPtr[1] = (byte)((i >> 8) & 0xFF);
            bufferPtr[2] = (byte)((i >> 16) & 0xFF);
            bufferPtr[3] = (byte)((i >> 24) & 0xFF);
            bufferPtr[4] = (byte)((i >> 32) & 0xFF);
            bufferPtr[5] = (byte)((i >> 40) & 0xFF);
            bufferPtr[6] = (byte)((i >> 48) & 0xFF);
            bufferPtr[7] = (byte)((i >> 56) & 0xFF);
        }

        public long PeekLongNetwork()
        {
            if (_buffer.Length - _offset < 8) { throw new ArgumentException("Index out of range.", nameof(_buffer)); }

            fixed (byte* bufferPtr = &_buffer[_offset])
            {
                return ((long)bufferPtr[0] << 56) |
                    ((long)bufferPtr[1] << 48) |
                    ((long)bufferPtr[2] << 40) |
                    ((long)bufferPtr[3] << 32) |
                    ((long)bufferPtr[4] << 24) |
                    ((long)bufferPtr[5] << 16) |
                    ((long)bufferPtr[6] << 8) |
                    ((long)bufferPtr[7] << 0);
            }
        }

        public long ReadLongNetwork()
        {
            var result = PeekLongNetwork();
            _offset += 8;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ReadLongNetwork(
            byte[] buffer, int offset)
        {
            if (buffer == null) { throw new ArgumentNullException(nameof(buffer)); }
            if (offset < 0) { throw new ArgumentOutOfRangeException(nameof(offset), offset, "Value must be positive."); }
            if (buffer.Length - offset < 8) { throw new ArgumentException("Index out of range.", nameof(buffer)); }

            fixed (byte* bufferPtr = &buffer[offset])
            {
                return ReadLongNetworkUnsafe(bufferPtr);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ReadLongNetworkUnsafe(
            byte[] buffer, int offset)
        {
            fixed (byte* bufferPtr = &buffer[offset])
            {
                return ReadLongNetworkUnsafe(bufferPtr);
            }
        }

        public static long ReadLongNetworkUnsafe(
            byte* bufferPtr)
        {
            return ((long)bufferPtr[0] << 56) |
                ((long)bufferPtr[1] << 48) |
                ((long)bufferPtr[2] << 40) |
                ((long)bufferPtr[3] << 32) |
                ((long)bufferPtr[4] << 24) |
                ((long)bufferPtr[5] << 16) |
                ((long)bufferPtr[6] << 8) |
                ((long)bufferPtr[7] << 0);
        }

        public void WriteLongNetwork(long i)
        {
            if (_buffer.Length - _offset < 8) { throw new ArgumentException("Index out of range.", nameof(_buffer)); }

            WriteLongNetworkUnsafe(_buffer, _offset, i);
            _offset += 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteLongNetwork(
            byte[] buffer, int offset, long i)
        {
            if (buffer == null) { throw new ArgumentNullException(nameof(buffer)); }
            if (offset < 0) { throw new ArgumentOutOfRangeException(nameof(offset), offset, "Value must be positive."); }
            if (buffer.Length - offset < 8) { throw new ArgumentException("Index out of range.", nameof(buffer)); }

            fixed (byte* bufferPtr = &buffer[offset])
            {
                WriteLongNetworkUnsafe(bufferPtr, i);
            }
        }

        public static void WriteLongNetworkUnsafe(
            byte[] buffer, int offset, long i)
        {
            fixed (byte* bufferPtr = &buffer[offset])
            {
                WriteLongNetworkUnsafe(bufferPtr, i);
            }
        }

        public static void WriteLongNetworkUnsafe(
            byte* bufferPtr, long i)
        {
            bufferPtr[0] = (byte)((i >> 56) & 0xFF);
            bufferPtr[1] = (byte)((i >> 48) & 0xFF);
            bufferPtr[2] = (byte)((i >> 40) & 0xFF);
            bufferPtr[3] = (byte)((i >> 32) & 0xFF);
            bufferPtr[4] = (byte)((i >> 24) & 0xFF);
            bufferPtr[5] = (byte)((i >> 16) & 0xFF);
            bufferPtr[6] = (byte)((i >> 8) & 0xFF);
            bufferPtr[7] = (byte)((i >> 0) & 0xFF);
        }

        public ulong PeekULong()
        {
            if (_buffer.Length - _offset < 8) { throw new ArgumentException("Index out of range.", nameof(_buffer)); }

            fixed (byte* bufferPtr = &_buffer[_offset])
            {
                return ((ulong)bufferPtr[0] << 0) |
                    ((ulong)bufferPtr[1] << 8) |
                    ((ulong)bufferPtr[2] << 16) |
                    ((ulong)bufferPtr[3] << 24) |
                    ((ulong)bufferPtr[4] << 32) |
                    ((ulong)bufferPtr[5] << 40) |
                    ((ulong)bufferPtr[6] << 48) |
                    ((ulong)bufferPtr[7] << 56);
            }
        }

        public ulong ReadULong()
        {
            var result = PeekULong();
            _offset += 8;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ReadULong(
            byte[] buffer, int offset)
        {
            if (buffer == null) { throw new ArgumentNullException(nameof(buffer)); }
            if (offset < 0) { throw new ArgumentOutOfRangeException(nameof(offset), offset, "Value must be positive."); }
            if (buffer.Length - offset < 8) { throw new ArgumentException("Index out of range.", nameof(buffer)); }

            fixed (byte* bufferPtr = &buffer[offset])
            {
                return ReadULongUnsafe(bufferPtr);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ReadULongUnsafe(
            byte[] buffer, int offset)
        {
            fixed (byte* bufferPtr = &buffer[offset])
            {
                return ReadULongUnsafe(bufferPtr);
            }
        }

        public static ulong ReadULongUnsafe(
            byte* bufferPtr)
        {
            return ((ulong)bufferPtr[0] << 0) |
                ((ulong)bufferPtr[1] << 8) |
                ((ulong)bufferPtr[2] << 16) |
                ((ulong)bufferPtr[3] << 24) |
                ((ulong)bufferPtr[4] << 32) |
                ((ulong)bufferPtr[5] << 40) |
                ((ulong)bufferPtr[6] << 48) |
                ((ulong)bufferPtr[7] << 56);
        }

        public void WriteULong(ulong i)
        {
            if (_buffer.Length - _offset < 8) { throw new ArgumentException("Index out of range.", nameof(_buffer)); }

            WriteULongUnsafe(_buffer, _offset, i);
            _offset += 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteULong(
            byte[] buffer, int offset, ulong i)
        {
            if (buffer == null) { throw new ArgumentNullException(nameof(buffer)); }
            if (offset < 0) { throw new ArgumentOutOfRangeException(nameof(offset), offset, "Value must be positive."); }
            if (buffer.Length - offset < 8) { throw new ArgumentException("Index out of range.", nameof(buffer)); }

            fixed (byte* bufferPtr = &buffer[offset])
            {
                WriteULongUnsafe(bufferPtr, i);
            }
        }

        public static void WriteULongUnsafe(
            byte[] buffer, int offset, ulong i)
        {
            fixed (byte* bufferPtr = &buffer[offset])
            {
                WriteULongUnsafe(bufferPtr, i);
            }
        }

        public static void WriteULongUnsafe(
            byte* bufferPtr, ulong i)
        {
            bufferPtr[0] = (byte)((i >> 0) & 0xFF);
            bufferPtr[1] = (byte)((i >> 8) & 0xFF);
            bufferPtr[2] = (byte)((i >> 16) & 0xFF);
            bufferPtr[3] = (byte)((i >> 24) & 0xFF);
            bufferPtr[4] = (byte)((i >> 32) & 0xFF);
            bufferPtr[5] = (byte)((i >> 40) & 0xFF);
            bufferPtr[6] = (byte)((i >> 48) & 0xFF);
            bufferPtr[7] = (byte)((i >> 56) & 0xFF);
        }

        public ulong PeekULongNetwork()
        {
            if (_buffer.Length - _offset < 8) { throw new ArgumentException("Index out of range.", nameof(_buffer)); }

            fixed (byte* bufferPtr = &_buffer[_offset])
            {
                return ((ulong)bufferPtr[0] << 56) |
                    ((ulong)bufferPtr[1] << 48) |
                    ((ulong)bufferPtr[2] << 40) |
                    ((ulong)bufferPtr[3] << 32) |
                    ((ulong)bufferPtr[4] << 24) |
                    ((ulong)bufferPtr[5] << 16) |
                    ((ulong)bufferPtr[6] << 8) |
                    ((ulong)bufferPtr[7] << 0);
            }
        }

        public ulong ReadULongNetwork()
        {
            var result = PeekULongNetwork();
            _offset += 8;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ReadULongNetwork(
            byte[] buffer, int offset)
        {
            if (buffer == null) { throw new ArgumentNullException(nameof(buffer)); }
            if (offset < 0) { throw new ArgumentOutOfRangeException(nameof(offset), offset, "Value must be positive."); }
            if (buffer.Length - offset < 8) { throw new ArgumentException("Index out of range.", nameof(buffer)); }

            fixed (byte* bufferPtr = &buffer[offset])
            {
                return ReadULongNetworkUnsafe(bufferPtr);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ReadULongNetworkUnsafe(
            byte[] buffer, int offset)
        {
            fixed (byte* bufferPtr = &buffer[offset])
            {
                return ReadULongNetworkUnsafe(bufferPtr);
            }
        }

        public static ulong ReadULongNetworkUnsafe(
            byte* bufferPtr)
        {
            return ((ulong)bufferPtr[0] << 56) |
                ((ulong)bufferPtr[1] << 48) |
                ((ulong)bufferPtr[2] << 40) |
                ((ulong)bufferPtr[3] << 32) |
                ((ulong)bufferPtr[4] << 24) |
                ((ulong)bufferPtr[5] << 16) |
                ((ulong)bufferPtr[6] << 8) |
                ((ulong)bufferPtr[7] << 0);
        }

        public void WriteULongNetwork(ulong i)
        {
            if (_buffer.Length - _offset < 8) { throw new ArgumentException("Index out of range.", nameof(_buffer)); }

            WriteULongNetworkUnsafe(_buffer, _offset, i);
            _offset += 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteULongNetwork(
            byte[] buffer, int offset, ulong i)
        {
            if (buffer == null) { throw new ArgumentNullException(nameof(buffer)); }
            if (offset < 0) { throw new ArgumentOutOfRangeException(nameof(offset), offset, "Value must be positive."); }
            if (buffer.Length - offset < 8) { throw new ArgumentException("Index out of range.", nameof(buffer)); }

            fixed (byte* bufferPtr = &buffer[offset])
            {
                WriteULongNetworkUnsafe(bufferPtr, i);
            }
        }

        public static void WriteULongNetworkUnsafe(
            byte[] buffer, int offset, ulong i)
        {
            fixed (byte* bufferPtr = &buffer[offset])
            {
                WriteULongNetworkUnsafe(bufferPtr, i);
            }
        }

        public static void WriteULongNetworkUnsafe(
            byte* bufferPtr, ulong i)
        {
            bufferPtr[0] = (byte)((i >> 56) & 0xFF);
            bufferPtr[1] = (byte)((i >> 48) & 0xFF);
            bufferPtr[2] = (byte)((i >> 40) & 0xFF);
            bufferPtr[3] = (byte)((i >> 32) & 0xFF);
            bufferPtr[4] = (byte)((i >> 24) & 0xFF);
            bufferPtr[5] = (byte)((i >> 16) & 0xFF);
            bufferPtr[6] = (byte)((i >> 8) & 0xFF);
            bufferPtr[7] = (byte)((i >> 0) & 0xFF);
        }

    }
}