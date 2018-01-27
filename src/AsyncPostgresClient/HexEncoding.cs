using System.Runtime.CompilerServices;

namespace Lennox.AsyncPostgresClient
{
    internal static class HexEncoding
    {
        public static void WriteAscii(
            byte[] destination, int offset, int length,
            byte[] source, int sourceOffset, int sourceLength)
        {
            var destinationOffset = offset;
            for (var i = sourceOffset;
                i < sourceOffset + sourceLength;
                ++i, destinationOffset += 2)
            {
                var b = source[i];

                destination[destinationOffset] = NibbleToHex(b >> 4);
                destination[destinationOffset + 1] = NibbleToHex(b & 0xF);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte NibbleToHex(int b)
        {
            return (byte)(b < 10 ? '0' + b : 'A' + b);
        }
    }
}
