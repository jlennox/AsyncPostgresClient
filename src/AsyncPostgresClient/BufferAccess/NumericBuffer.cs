using System;
using System.Runtime.CompilerServices;
using System.Text;
using Lennox.AsyncPostgresClient.Diagnostic;

namespace Lennox.AsyncPostgresClient.BufferAccess
{
    // TODO: Look into https://johnnylee-sde.github.io/Fast-numeric-string-to-int/
    internal static class NumericBuffer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool TryAsciiToInt(
            byte[] data, int offset, int length, out int number)
        {
            if (length == 0)
            {
                number = 0;
                return true;
            }

            fixed (byte* pt = &data[offset])
            {
                return TryAsciiToInt(pt, length, out number);
            }
        }

       public static unsafe bool TryAsciiToInt(
            byte* pt, int length, out int number)
        {
            switch (length)
            {
                case 0:
                    number = 0;
                    return true;
                case 1:
                    number = pt[0] & 0xF;
                    return true;
                case 2:
                    number = (pt[0] & 0xF) * 10 +
                        (pt[1] & 0xF);
                    return true;
                case 3:
                    number = (pt[0] & 0xF) * 100 +
                        (pt[1] & 0xF) * 10 +
                        (pt[2] & 0xF);
                    return true;
                case 4:
                    number = (pt[0] & 0xF) * 1000 +
                        (pt[1] & 0xF) * 100 +
                        (pt[2] & 0xF) * 10 +
                        (pt[3] & 0xF);
                    return true;
                case 5:
                    number = (pt[0] & 0xF) * 10000 +
                        (pt[1] & 0xF) * 1000 +
                        (pt[2] & 0xF) * 100 +
                        (pt[3] & 0xF) * 10 +
                        (pt[4] & 0xF);
                    return true;
                case 6:
                    number = (pt[0] & 0xF) * 100000 +
                        (pt[1] & 0xF) * 10000 +
                        (pt[2] & 0xF) * 1000 +
                        (pt[3] & 0xF) * 100 +
                        (pt[4] & 0xF) * 10 +
                        (pt[5] & 0xF);
                    return true;
                case 7:
                    number = (pt[0] & 0xF) * 1000000 +
                        (pt[1] & 0xF) * 100000 +
                        (pt[2] & 0xF) * 10000 +
                        (pt[3] & 0xF) * 1000 +
                        (pt[4] & 0xF) * 100 +
                        (pt[5] & 0xF) * 10 +
                        (pt[6] & 0xF);
                    return true;
                case 8:
                    number = (pt[0] & 0xF) * 10000000 +
                        (pt[1] & 0xF) * 1000000 +
                        (pt[2] & 0xF) * 100000 +
                        (pt[3] & 0xF) * 10000 +
                        (pt[4] & 0xF) * 1000 +
                        (pt[5] & 0xF) * 100 +
                        (pt[6] & 0xF) * 10 +
                        (pt[7] & 0xF);
                    return true;
                case 9:
                    number = (pt[0] & 0xF) * 100000000 +
                        (pt[1] & 0xF) * 10000000 +
                        (pt[2] & 0xF) * 1000000 +
                        (pt[3] & 0xF) * 100000 +
                        (pt[4] & 0xF) * 10000 +
                        (pt[5] & 0xF) * 1000 +
                        (pt[6] & 0xF) * 100 +
                        (pt[7] & 0xF) * 10 +
                        (pt[8] & 0xF);
                    return true;
                case 10:
                    number = (pt[0] & 0xF) * 1000000000 +
                        (pt[1] & 0xF) * 100000000 +
                        (pt[2] & 0xF) * 10000000 +
                        (pt[3] & 0xF) * 1000000 +
                        (pt[4] & 0xF) * 100000 +
                        (pt[5] & 0xF) * 10000 +
                        (pt[6] & 0xF) * 1000 +
                        (pt[7] & 0xF) * 100 +
                        (pt[8] & 0xF) * 10 +
                        (pt[9] & 0xF);
                    return true;
            }

            number = 0;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short AsciiToShort(byte[] data)
        {
            if (TryAsciiToInt(data, 0, 2, out var number))
            {
                return (short)number;
            }

            throw new ArgumentOutOfRangeException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short AsciiToShort(byte[] data, int length)
        {
            if (TryAsciiToInt(data, 0, length, out var number))
            {
                return (short)number;
            }

            throw new ArgumentOutOfRangeException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AsciiToInt(byte[] data)
        {
            if (TryAsciiToInt(data, 0, data.Length, out var number))
            {
                return number;
            }

            throw new ArgumentOutOfRangeException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AsciiToInt(byte[] data, int offset, int length)
        {
            if (TryAsciiToInt(data, offset, length, out var number))
            {
                return number;
            }

            throw new ArgumentOutOfRangeException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long AsciiToLong(byte[] data)
        {
            return AsciiToLong(data, 0, data.Length);
        }

        public static unsafe long AsciiToLong(
            byte[] data, int offset, int length)
        {
            if (length == 0)
            {
                return 0;
            }

            if (TryAsciiToInt(data, offset, length, out var number))
            {
                return number;
            }

            if (offset + length > data.Length)
            {
                // TODO
                throw new ArgumentOutOfRangeException();
            }

            long sum = 0;

            fixed (byte* pt = &data[offset])
            {
                for (var i = 0; i < length; ++i)
                {
                    sum = (sum * 10) + (pt[i] & 0xF);
                }
            }

            return sum;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal AsciiToDecimal(byte[] data)
        {
            return AsciiToDecimal(data, data.Length);
        }

        public static unsafe decimal AsciiToDecimal(byte[] data, int length)
        {
            const byte period = (byte)'.';
            const byte plus = (byte)'+';
            const byte e = (byte)'e';

            if (DebugLogger.Enabled)
            {
                DebugLogger.Log("AsciiToDecimal {0}",
                    Encoding.ASCII.GetString(data, 0, length));
            }

            if (length == 0)
            {
                return 0;
            }

            var periodPosition = -1;
            var plusPosition = -1;
            var plusStart = -1;
            var hasPlus = false;

            fixed (byte* pt = data)
            {
                var wasE = false;

                for (var i = 0; i < length; ++i)
                {
                    var chr = pt[i];

                    switch (chr)
                    {
                        case period:
                            if (wasE)
                            {
                                throw new InvalidOperationException(
                                    $"Unexpected character in input at position {i - 1}.");
                            }

                            if (periodPosition != -1)
                            {
                                throw new InvalidOperationException(
                                    $"Unexpected second peroid in input at position {i}.");
                            }

                            if (plusPosition != -1)
                            {
                                throw new InvalidOperationException(
                                    $"Unexpected peroid after plus at position {i}.");
                            }

                            periodPosition = i;
                            continue;
                        case e:
                            wasE = true;
                            continue;
                        case plus:
                            if (plusPosition != -1)
                            {
                                throw new InvalidOperationException(
                                    $"Unexpected second plus in input at position {i}.");
                            }

                            if (wasE)
                            {
                                wasE = false;
                                plusStart = i - 1;
                            }
                            else
                            {
                                plusStart = i;
                            }

                            hasPlus = true;
                            plusPosition = i;
                            continue;
                    }

                    if (wasE)
                    {
                        throw new InvalidOperationException(
                            $"Unexpected character in input at position {i - 1}.");
                    }

                    if (!char.IsNumber((char)chr))
                    {
                        throw new InvalidOperationException(
                            $"Unexpected non-numeric input 0x{chr:X2} at position {i}.");
                    }
                }
            }

            var digitEnd = 0;
            var decimalEnd = 0;

            if (periodPosition == -1 && !hasPlus)
            {
                digitEnd = data.Length;
            }
            else if (periodPosition == -1 && hasPlus)
            {
                digitEnd = plusStart;
            }
            else if (periodPosition != -1 && !hasPlus)
            {
                digitEnd = periodPosition;
                decimalEnd = data.Length;
            }
            else if (periodPosition != -1 && hasPlus)
            {
                digitEnd = periodPosition;
                decimalEnd = plusStart;
            }

            var exponent = 0;

            if (hasPlus)
            {
                exponent = AsciiToInt(data, plusPosition + 1,
                    data.Length - plusPosition - 1);
            }

            if (periodPosition == -1)
            {
                var val = AsciiToLong(data, 0, digitEnd);

                return hasPlus ? (decimal)Math.Pow(val, exponent) : val;
            }

            var fractionStart = periodPosition + 1;
            var fractionLength = decimalEnd - fractionStart;

            decimal whole = AsciiToLong(data, 0, periodPosition);

            // Catch unneeded trailing periods. "500."
            if (fractionStart >= length)
            {
                return hasPlus ? whole * (decimal)Math.Pow(10, exponent) : whole;
            }

            decimal fraction = AsciiToLong(
                data, fractionStart, fractionLength);

            decimal withFraction;

            switch (fractionLength)
            {
                case 0:  withFraction = whole; break;
                case 1:  withFraction = whole + fraction * .1m; break;
                case 2:  withFraction = whole + fraction * .01m; break;
                case 3:  withFraction = whole + fraction * .001m; break;
                case 4:  withFraction = whole + fraction * .0001m; break;
                case 5:  withFraction = whole + fraction * .00001m; break;
                case 6:  withFraction = whole + fraction * .000001m; break;
                case 7:  withFraction = whole + fraction * .0000001m; break;
                case 8:  withFraction = whole + fraction * .00000001m; break;
                case 9:  withFraction = whole + fraction * .000000001m; break;
                case 10: withFraction = whole + fraction * .0000000001m; break;
                default:
                    // TODO
                    for (var i = 0; i < fractionLength; ++i)
                    {
                        fraction *= .1m;
                    }

                    withFraction = whole + fraction;
                    break;
            }

            return hasPlus
                ? withFraction * (decimal)Math.Pow(10, exponent)
                : withFraction;
        }
    }
}
