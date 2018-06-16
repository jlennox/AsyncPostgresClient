using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Lennox.AsyncPostgresClient.BufferAccess;

namespace Lennox.AsyncPostgresClient.PostgresTypes
{

    [AttributeUsage(AttributeTargets.Class)]
    internal class PostgresTypeConverterMethodAttribute : Attribute
    {
        public string[] PostgresTypeNames { get; }

        public PostgresTypeConverterMethodAttribute(
            params string[] postgresTypeNames)
        {
            PostgresTypeNames = postgresTypeNames;
        }
    }

    internal interface IPostgresTypeCodec
    {
        Type SystemType { get; set; }

        object DecodeBinaryObject(DataRow row, PostgresClientState state);
        object DecodeTextObject(DataRow row, PostgresClientState state);
    }

    internal abstract class PostgresTypeCodec<T> : IPostgresTypeCodec
    {
        public Type SystemType { get; set; } = typeof(T);

        public abstract IReadOnlyList<T> DecodeBinaryArray(
            DataRow row, PostgresClientState state);
        public abstract T DecodeBinary(DataRow row, PostgresClientState state);
        public abstract T DecodeText(DataRow row, PostgresClientState state);
        public abstract void EncodeBinary(MemoryStream ms, T value, PostgresClientState state);
        public abstract void EncodeText(MemoryStream ms, T value, PostgresClientState state);

        public object DecodeBinaryObject(
            DataRow row,
            PostgresClientState state)
        {
            return DecodeBinary(row, state);
        }

        public object DecodeTextObject(
            DataRow row,
            PostgresClientState state)
        {
            return DecodeText(row, state);
        }

        public T Decode(
            DataRow row,
            PostgresFormatCode formatCode,
            PostgresClientState state)
        {
            switch (formatCode)
            {
                case PostgresFormatCode.Text:
                    return DecodeText(row, state);
                case PostgresFormatCode.Binary:
                    return DecodeBinary(row, state);
            }

            throw new ArgumentOutOfRangeException(
                nameof(formatCode), formatCode,
                "Unknown format code.");
        }

        protected int GetInt4(DataRow row)
        {
            return BinaryBuffer.ReadIntNetwork(row.Data, 0);
        }
    }

    [PostgresTypeConverterMethod(PostgresTypeNames.Bool)]
    internal class PostgresBoolCodec : PostgresTypeCodec<bool>
    {
        public static readonly PostgresBoolCodec Default = new PostgresBoolCodec();

        public override unsafe IReadOnlyList<bool> DecodeBinaryArray(
            DataRow row, PostgresClientState state)
        {
            if (row.Length == 0)
            {
                return EmptyArray<bool>.Value;
            }

            var result = new bool[row.Length];

            fixed (bool* resultPtr = result)
            fixed (byte* dataPtr = row.Data)
            {
                for (var i = 0; i < row.Length; ++i)
                {
                    switch (dataPtr[i])
                    {
                        case 0: resultPtr[i] = false; break;
                        case 1: resultPtr[i] = true; break;
                        default: throw new ArgumentOutOfRangeException();
                    }

                }
            }

            return result;
        }

        public override bool DecodeBinary(DataRow row, PostgresClientState state)
        {
            PostgresTypeConverter.DemandDataLength(row, 1);

            switch (row.Data[0])
            {
                case 0: return false;
                case 1: return true;
            }

            throw new ArgumentOutOfRangeException();
        }

        public override bool DecodeText(DataRow row, PostgresClientState state)
        {
            PostgresTypeConverter.DemandDataLength(row, 1);

            switch (row.Data[0])
            {
                case (byte)'f': return false;
                case (byte)'t': return true;
            }

            throw new ArgumentOutOfRangeException();
        }

        public override void EncodeBinary(
            MemoryStream ms, bool value, PostgresClientState state)
        {
            throw new NotImplementedException();
        }

        public override void EncodeText(
            MemoryStream ms, bool value, PostgresClientState state)
        {
            throw new NotImplementedException();
        }
    }

    [PostgresTypeConverterMethod(PostgresTypeNames.Text, PostgresTypeNames.VarChar)]
    internal class PostgresStringCodec : PostgresTypeCodec<string>
    {
        public static readonly PostgresStringCodec Default = new PostgresStringCodec();

        public override unsafe IReadOnlyList<string> DecodeBinaryArray(
            DataRow row, PostgresClientState state)
        {
            if (row.Data.Length == 0)
            {
                return EmptyArray<string>.Value;
            }

            var results = new List<string>();

            fixed (byte* dataPtr = row.Data)
            for (var i = 0; i < row.Length;)
            {
                var size = BinaryBuffer.ReadIntNetworkUnsafe(&dataPtr[i]);

                if (size + i > row.Length)
                {
                    throw new ArgumentOutOfRangeException();
                }

                var s = state.ServerEncoding.GetString(&dataPtr[i + 4], size);
                results.Add(s);
            }

            return results;
        }

        public override string DecodeBinary(DataRow row, PostgresClientState state)
        {
            if (row.Length == 0)
            {
                return "";
            }

            return state.ServerEncoding.GetString(row.Data, 0, row.Length);
        }

        public override string DecodeText(DataRow row, PostgresClientState state)
        {
            return DecodeBinary(row, state);
        }

        public override void EncodeBinary(
            MemoryStream ms, string value, PostgresClientState state)
        {
            throw new NotImplementedException();
        }

        public override void EncodeText(
            MemoryStream ms, string value, PostgresClientState state)
        {
            throw new NotImplementedException();
        }
    }

    [PostgresTypeConverterMethod(PostgresTypeNames.Int2)]
    internal class PostgresShortCodec : PostgresTypeCodec<short>
    {
        public static readonly PostgresShortCodec Default = new PostgresShortCodec();

        public override unsafe IReadOnlyList<short> DecodeBinaryArray(
            DataRow row, PostgresClientState state)
        {
            const int length = 2;

            if (row.Length == 0)
            {
                return EmptyArray<short>.Value;
            }

            var result = new short[row.Length / length];
            var resultIndex = 0;

            fixed (short* resultPtr = result)
            fixed (byte* dataPtr = row.Data)
            {
                for (var i = 0; i < row.Length; i += length)
                {
                    resultPtr[resultIndex++] = BinaryBuffer
                        .ReadShortNetworkUnsafe(&dataPtr[i]);
                }
            }

            return result;
        }

        public override short DecodeBinary(DataRow row, PostgresClientState state)
        {
            PostgresTypeConverter.DemandDataLength(row, 2);

            return BinaryBuffer.ReadShortNetworkUnsafe(row.Data, 0);
        }

        public override short DecodeText(DataRow row, PostgresClientState state)
        {
            return NumericBuffer.AsciiToShort(row.Data, row.Length);
        }

        public override void EncodeBinary(
            MemoryStream ms, short value, PostgresClientState state)
        {
            throw new NotImplementedException();
        }

        public override void EncodeText(
            MemoryStream ms, short value, PostgresClientState state)
        {
            throw new NotImplementedException();
        }
    }

    [PostgresTypeConverterMethod(PostgresTypeNames.Int4)]
    internal class PostgresIntCodec : PostgresTypeCodec<int>
    {
        public static readonly PostgresIntCodec Default = new PostgresIntCodec();

        public override unsafe IReadOnlyList<int> DecodeBinaryArray(
            DataRow row, PostgresClientState state)
        {
            const int length = 4;

            if (row.Length == 0)
            {
                return EmptyArray<int>.Value;
            }

            var result = new int[row.Length / length];
            var resultIndex = 0;

            fixed (int* resultPtr = result)
            fixed (byte* dataPtr = row.Data)
            {
                for (var i = 0; i < row.Length; i += length)
                {
                    resultPtr[resultIndex++] = BinaryBuffer
                        .ReadIntNetworkUnsafe(&dataPtr[i]);
                }
            }

            return result;
        }

        public override int DecodeBinary(DataRow row, PostgresClientState state)
        {
            PostgresTypeConverter.DemandDataLength(row, 4);

            return BinaryBuffer.ReadIntNetworkUnsafe(row.Data, 0);
        }

        public override int DecodeText(DataRow row, PostgresClientState state)
        {
            return NumericBuffer.AsciiToInt(row.Data, 0, row.Length);
        }

        public override void EncodeBinary(
            MemoryStream ms, int value, PostgresClientState state)
        {
            throw new NotImplementedException();
        }

        public override void EncodeText(
            MemoryStream ms, int value, PostgresClientState state)
        {
            throw new NotImplementedException();
        }
    }

    [PostgresTypeConverterMethod(PostgresTypeNames.Int8)]
    internal class PostgresLongCodec : PostgresTypeCodec<long>
    {
        public static readonly PostgresLongCodec Default = new PostgresLongCodec();

        public override unsafe IReadOnlyList<long> DecodeBinaryArray(
            DataRow row, PostgresClientState state)
        {
            const int length = 8;

            if (row.Length == 0)
            {
                return EmptyArray<long>.Value;
            }

            var result = new long[row.Length / length];
            var resultIndex = 0;

            fixed (long* resultPtr = result)
            fixed (byte* dataPtr = row.Data)
            {
                for (var i = 0; i < row.Length; i += length)
                {
                    resultPtr[resultIndex++] = BinaryBuffer
                        .ReadLongNetworkUnsafe(&dataPtr[i]);
                }
            }

            return result;
        }

        public override long DecodeBinary(DataRow row, PostgresClientState state)
        {
            PostgresTypeConverter.DemandDataLength(row, 8);

            return BinaryBuffer.ReadLongNetworkUnsafe(row.Data, 0);
        }

        public override long DecodeText(DataRow row, PostgresClientState state)
        {
            return NumericBuffer.AsciiToLong(row.Data, 0, row.Length);
        }

        public override void EncodeBinary(
            MemoryStream ms, long value, PostgresClientState state)
        {
            throw new NotImplementedException();
        }

        public override void EncodeText(
            MemoryStream ms, long value, PostgresClientState state)
        {
            throw new NotImplementedException();
        }
    }

    [PostgresTypeConverterMethod(PostgresTypeNames.Uuid)]
    internal class PostgresGuidCodec : PostgresTypeCodec<Guid>
    {
        public static readonly PostgresGuidCodec Default = new PostgresGuidCodec();

        public override unsafe IReadOnlyList<Guid> DecodeBinaryArray(
            DataRow row, PostgresClientState state)
        {
            const int length = 16;

            if (row.Length == 0)
            {
                return EmptyArray<Guid>.Value;
            }

            var result = new Guid[row.Length / length];
            var resultIndex = 0;

            fixed (Guid* resultPtr = result)
            fixed (byte* dataPtr = row.Data)
            {
                for (var i = 0; i < row.Length; i += length)
                {
                    resultPtr[resultIndex++] = ReadBinaryGuid(&dataPtr[i]);
                }
            }

            return result;
        }

        public override unsafe Guid DecodeBinary(
            DataRow row, PostgresClientState state)
        {
            PostgresTypeConverter.DemandDataLength(row, 16);

            fixed (byte* data = row.Data)
            {
                return ReadBinaryGuid(data);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe Guid ReadBinaryGuid(byte* data)
        {
            var a = BinaryBuffer.ReadIntNetworkUnsafe(data);
            var b = BinaryBuffer.ReadShortNetworkUnsafe(&data[4]);
            var c = BinaryBuffer.ReadShortNetworkUnsafe(&data[6]);
            return new Guid(a, b, c, data[8], data[9], data[10],
                data[11], data[12], data[13], data[14], data[15]);
        }

        public override Guid DecodeText(DataRow row, PostgresClientState state)
        {
            // TODO: Get rid of the string allocation here.
            var guidString = PostgresStringCodec.Default
                .DecodeText(row, state);
            return new Guid(guidString);
        }

        public override void EncodeBinary(
            MemoryStream ms, Guid value, PostgresClientState state)
        {
            throw new NotImplementedException();
        }

        public override void EncodeText(
            MemoryStream ms, Guid value, PostgresClientState state)
        {
            throw new NotImplementedException();
        }
    }

    [PostgresTypeConverterMethod(PostgresTypeNames.Date)]
    internal class PostgresDateTimeCodec : PostgresTypeCodec<DateTime>
    {
        public static readonly PostgresDateTimeCodec Default = new PostgresDateTimeCodec();

        public override IReadOnlyList<DateTime> DecodeBinaryArray(
            DataRow row, PostgresClientState state)
        {
            throw new NotImplementedException();
        }

        public override DateTime DecodeBinary(DataRow row, PostgresClientState state)
        {
            var date = GetInt4(row);
            return PostgresDateTime.FromJulian(
                date + PostgresDateTime.PostgresEpochJdate);
        }

        public override DateTime DecodeText(DataRow row, PostgresClientState state)
        {
            throw new NotImplementedException();
        }

        public override void EncodeBinary(
            MemoryStream ms, DateTime value, PostgresClientState state)
        {
            throw new NotImplementedException();
        }

        public override void EncodeText(
            MemoryStream ms, DateTime value, PostgresClientState state)
        {
            throw new NotImplementedException();
        }
    }

    [PostgresTypeConverterMethod(PostgresTypeNames.Timestamp)]
    internal class PostgresTimeSpanCodec : PostgresTypeCodec<DateTime>
    {
        public static readonly PostgresTimeSpanCodec Default = new PostgresTimeSpanCodec();

        public override IReadOnlyList<DateTime> DecodeBinaryArray(
            DataRow row, PostgresClientState state)
        {
            throw new NotImplementedException();
        }

        public override DateTime DecodeBinary(DataRow row, PostgresClientState state)
        {
            throw new NotImplementedException();
        }

        public override DateTime DecodeText(DataRow row, PostgresClientState state)
        {
            throw new NotImplementedException();
        }

        public override void EncodeBinary(
            MemoryStream ms, DateTime value, PostgresClientState state)
        {
            throw new NotImplementedException();
        }

        public override void EncodeText(
            MemoryStream ms, DateTime value, PostgresClientState state)
        {
            throw new NotImplementedException();
        }
    }

    [PostgresTypeConverterMethod(PostgresTypeNames.Numeric)]
    internal class PostgresDecimalCodec : PostgresTypeCodec<decimal>
    {
        public static readonly PostgresDecimalCodec Default = new PostgresDecimalCodec();

        public override IReadOnlyList<decimal> DecodeBinaryArray(
            DataRow row, PostgresClientState state)
        {
            throw new NotImplementedException();
        }

        public override decimal DecodeBinary(DataRow row, PostgresClientState state)
        {
            throw new NotImplementedException();
        }

        public override decimal DecodeText(DataRow row, PostgresClientState state)
        {
            return NumericBuffer.AsciiToDecimal(row.Data, row.Length);
        }

        public override void EncodeBinary(
            MemoryStream ms, decimal value, PostgresClientState state)
        {
            throw new NotImplementedException();
        }

        public override void EncodeText(
            MemoryStream ms, decimal value, PostgresClientState state)
        {
            throw new NotImplementedException();
        }
    }

    // real | 4 bytes | variable-precision, inexact | 6 decimal digits precision
    [PostgresTypeConverterMethod(PostgresTypeNames.Float4)]
    internal class PostgresFloatCodec : PostgresTypeCodec<float>
    {
        public static readonly PostgresFloatCodec Default = new PostgresFloatCodec();

        public override IReadOnlyList<float> DecodeBinaryArray(
            DataRow row, PostgresClientState state)
        {
            throw new NotImplementedException();
        }

        public override unsafe float DecodeBinary(DataRow row, PostgresClientState state)
        {
            PostgresTypeConverter.DemandDataLength(row, 4);

            var asint = BinaryBuffer.ReadIntNetwork(row.Data, 0);
            return *(float*)&asint;
        }

        public override float DecodeText(DataRow row, PostgresClientState state)
        {
            return (float)NumericBuffer.AsciiToDecimal(row.Data, row.Length);
        }

        public override void EncodeBinary(
            MemoryStream ms, float value, PostgresClientState state)
        {
            throw new NotImplementedException();
        }

        public override void EncodeText(
            MemoryStream ms, float value, PostgresClientState state)
        {
            throw new NotImplementedException();
        }
    }

    // double precision | 8 bytes | variable-precision, inexact | 15 decimal digits precision
    [PostgresTypeConverterMethod(PostgresTypeNames.Float8)]
    internal class PostgresDoubleCodec : PostgresTypeCodec<double>
    {
        public static readonly PostgresDoubleCodec Default = new PostgresDoubleCodec();

        public override IReadOnlyList<double> DecodeBinaryArray(
            DataRow row, PostgresClientState state)
        {
            throw new NotImplementedException();
        }

        public override unsafe double DecodeBinary(DataRow row, PostgresClientState state)
        {
            PostgresTypeConverter.DemandDataLength(row, 8);

            var asint = BinaryBuffer.ReadLongNetwork(row.Data, 0);
            return *(double*)&asint;
        }

        public override double DecodeText(DataRow row, PostgresClientState state)
        {
            return (double)NumericBuffer.AsciiToDecimal(row.Data, row.Length);
        }

        public override void EncodeBinary(
            MemoryStream ms, double value, PostgresClientState state)
        {
            throw new NotImplementedException();
        }

        public override void EncodeText(
            MemoryStream ms, double value, PostgresClientState state)
        {
            throw new NotImplementedException();
        }
    }
}