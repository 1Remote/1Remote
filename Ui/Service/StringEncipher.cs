using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NETCore.Encrypt;

namespace _1RM.Service
{
    public static class StringEncipher
    {
        private const string SimpleAesKey = "9ho5kUf2UVbugom7NME8ZVxFZZjavHej";
        public static string SimpleEncrypt(string txt)
        {
            return EncryptProvider.AESEncrypt(txt, SimpleAesKey);
        }
        public static string SimpleDecrypt(string encryptString)
        {
            return EncryptProvider.AESDecrypt(encryptString, SimpleAesKey); ;
        }
    }
}
