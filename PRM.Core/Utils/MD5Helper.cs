using System;
using System.Security.Cryptography;
using System.Text;

namespace Shawn.Utils
{
    public class MD5Helper
    {
        public static byte[] GetMd5Hash32Bit(byte[] data)
        {
            MD5 md5 = MD5.Create();
            byte[] outBytes = md5.ComputeHash(data);
            return outBytes;
        }

        public static string GetMd5Hash32BitString(byte[] data)
        {
            byte[] outBytes = GetMd5Hash32Bit(data);
            string outString = string.Empty;
            foreach (byte t in outBytes)
            {
                outString += t.ToString("x2");
            }
            return outString;
        }

        public static string GetMd5Hash16BitString(byte[] data)
        {
            string outString = BitConverter.ToString(GetMd5Hash32Bit(data), 4, 8);
            outString = outString.Replace("-", "");
            return outString;
        }

        public static byte[] GetMd5Hash32Bit(string str)
        {
            return GetMd5Hash32Bit(Encoding.UTF8.GetBytes(str));
        }

        public static string GetMd5Hash32BitString(string str)
        {
            return GetMd5Hash32BitString(Encoding.UTF8.GetBytes(str));
        }

        public static string GetMd5Hash16BitString(string str)
        {
            return GetMd5Hash16BitString(Encoding.UTF8.GetBytes(str));
        }
    }
}