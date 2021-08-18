using System;
using System.Text;

namespace SindenLib
{
    internal static class Extensions
    {
        public static string ToHex(this byte[] array)
        {
            Span<char> c = stackalloc char[array.Length * 2];

            byte b;
            for (var i = 0; i < array.Length; ++i)
            {
                b = (byte)(array[i] >> 4);
                c[i * 2] = (char)(b > 9 ? b + 0x37 : b + 0x30);
                b = (byte)(array[i] & 0xF);
                c[i * 2 + 1] = (char)(b > 9 ? b + 0x37 : b + 0x30);
            }

            return new string(c).ToLowerInvariant();
        }

        public static byte[] ToBytes(this string value)
        {
            return Encoding.ASCII.GetBytes(value);
        }

        public static bool IsEqual<T>(this T[] src, T[] value, int length = -1)
        {
            if (src == null || value == null)
                return src == value;

            if (src.Length != value.Length)
                return false;

            if (length < 0 || length > src.Length)
                length = src.Length;

            for (var i = 0; i < length; i++)
                if (!src[i].Equals(value[i]))
                    return false;

            return true;
        }
    }
}