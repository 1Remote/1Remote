using System;
using System.Text;

namespace _1RM.Utils
{
    public static class UnSafeStringEncipher
    {
        private static readonly Random Random = new Random();
        public static string SimpleEncrypt(string txt)
        {
            if (string.IsNullOrEmpty(txt))
                return string.Empty;
            var key = new StringBuilder();
            for (int i = 0; i < 32; i++)
            {
                key.Append((char)Random.Next('0', 'z'));
            }
            return key.ToString() + EasyEncryption.AesThenHmac.SimpleEncryptWithPassword(txt, key.ToString());
        }
        public static string SimpleDecrypt(string encryptString)
        {
            if (string.IsNullOrEmpty(encryptString))
                return string.Empty;
            if(encryptString.Length <= 32)
                return string.Empty;

            var key = encryptString.Substring(0, 32);
            var str = encryptString.Substring(32);
            try
            {
                return EasyEncryption.AesThenHmac.SimpleDecryptWithPassword(str, key);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}
