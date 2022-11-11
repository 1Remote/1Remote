using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NETCore.Encrypt;
using NETCore.Encrypt.Extensions;
using SshNet.Security.Cryptography;

namespace _1RM.Service
{
    public static class UnSafeStringEncipher
    {
        private static Random _random = new Random();
        public static string SimpleEncrypt(string txt)
        {
            if (string.IsNullOrEmpty(txt))
                return string.Empty;
            var key = new StringBuilder();
            for (int i = 0; i < 32; i++)
            {
                key.Append((char)_random.Next('0', 'z'));
            }
            return key.ToString() + EncryptProvider.AESEncrypt(txt, key.ToString());
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
                return EncryptProvider.AESDecrypt(str, key);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}
