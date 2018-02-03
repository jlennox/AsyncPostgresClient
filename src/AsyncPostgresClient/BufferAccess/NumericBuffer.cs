using System;
using System.Collections.Generic;
using System.Text;

namespace Lennox.AsyncPostgresClient.BufferAccess
{
    // TODO: Look into https://johnnylee-sde.github.io/Fast-numeric-string-to-int/
    internal static class NumericBuffer
    {
        public static unsafe bool TryAsciiToInt(
            byte[] data, int offset, int length, out int number)
        {
            switch (length)
            {
                case 0:
                    number = 0;
                    return true;
                case 1:
                    number = data[offset] & 0xF;
                    return true;
                case 2:
                    fixed (byte* pt = &data[offset])
                    {
                        number = (pt[0] & 0xF) * 10 +
                            (pt[1] & 0xF);
                        return true;
                    }
                case 3:
                    fixed (byte* pt = &data[offset])
                    {
                        number = (pt[0] & 0xF) * 100 +
                            (pt[1] & 0xF) * 10 +
                            (pt[2] & 0xF);
                        return true;
                    }
                case 4:
                    fixed (byte* pt = &data[offset])
                    {
                        number = (pt[0] & 0xF) * 1000 +
                            (pt[1] & 0xF) * 100 +
                            (pt[2] & 0xF) * 10 +
                            (pt[3] & 0xF);
                        return true;
                    }
                case 5:
                    fixed (byte* pt = &data[offset])
                    {
                        number = (pt[0] & 0xF) * 10000 +
                            (pt[1] & 0xF) * 1000 +
                            (pt[2] & 0xF) * 100 +
                            (pt[3] & 0xF) * 10 +
                            (pt[4] & 0xF);
                        return true;
                    }
                case 6:
                    fixed (byte* pt = &data[offset])
                    {
                        number = (pt[0] & 0xF) * 100000 +
                            (pt[1] & 0xF) * 10000 +
                            (pt[2] & 0xF) * 1000 +
                            (pt[3] & 0xF) * 100 +
                            (pt[4] & 0xF) * 10 +
                            (pt[5] & 0xF);
                        return true;
                    }
                case 7:
                    fixed (byte* pt = &data[offset])
                    {
                        number = (pt[0] & 0xF) * 1000000 +
                            (pt[1] & 0xF) * 100000 +
                            (pt[2] & 0xF) * 10000 +
                            (pt[3] & 0xF) * 1000 +
                            (pt[4] & 0xF) * 100 +
                            (pt[5] & 0xF) * 10 +
                            (pt[6] & 0xF);
                        return true;
                    }
                case 8:
                    fixed (byte* pt = &data[offset])
                    {
                        number = (pt[0] & 0xF) * 10000000 +
                            (pt[1] & 0xF) * 1000000 +
                            (pt[2] & 0xF) * 100000 +
                            (pt[3] & 0xF) * 10000 +
                            (pt[4] & 0xF) * 1000 +
                            (pt[5] & 0xF) * 100 +
                            (pt[6] & 0xF) * 10 +
                            (pt[7] & 0xF);
                        return true;
                    }
                case 9:
                    fixed (byte* pt = &data[offset])
                    {
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
                    }
                case 10:
                    fixed (byte* pt = &data[offset])
                    {
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
            }

            number = 0;
            return false;
        }

        public static int AsciiToInt(byte[] data)
        {
            if (TryAsciiToInt(data, 0, data.Length, out var number))
            {
                return number;
            }

            throw new ArgumentOutOfRangeException();
        }

        public static long AsciiToLong(byte[] data)
        {
            return AsciiToLong(data, 0, data.Length);
        }

        public static unsafe long AsciiToLong(
            byte[] data, int offset, int length)
        {
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

        public static unsafe decimal AsciiToDecimal(byte[] data)
        {
            const byte period = (byte)'.';

            var periodPosition = -1;

            fixed (byte* pt = data)
            {
                for (var i = 0; i < data.Length; ++i)
                {
                    if (pt[i] == period)
                    {
                        periodPosition = i;
                        break;
                    }
                }
            }

            if (periodPosition == -1)
            {
                return AsciiToLong(data);
            }

            var fractionStart = periodPosition + 1;
            var fractionLength = data.Length - fractionStart;

            decimal whole = AsciiToLong(data, 0, periodPosition);

            // Catch unneeded trailing periods. "500."
            if (fractionStart >= data.Length)
            {
                return whole;
            }

            decimal fraction = AsciiToLong(
                data, fractionStart, fractionLength);

            switch (fractionLength)
            {
                case 0:  return whole;
                case 1:  return whole + fraction * .1m;
                case 2:  return whole + fraction * .01m;
                case 3:  return whole + fraction * .001m;
                case 4:  return whole + fraction * .0001m;
                case 5:  return whole + fraction * .00001m;
                case 6:  return whole + fraction * .000001m;
                case 7:  return whole + fraction * .0000001m;
                case 8:  return whole + fraction * .00000001m;
                case 9:  return whole + fraction * .000000001m;
                case 10: return whole + fraction * .0000000001m;
            }

            // TODO
            for (var i = 0; i < fractionLength; ++i)
            {
                fraction *= .1m;
            }

            return whole + fraction;
        }
    }
}
