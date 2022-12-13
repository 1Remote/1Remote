using System;
using System.Text;

namespace _1RM.Utils
{
    public static class UnSafeStringEncipher
    {
        private static string? _salt = null;
        public static void Init(string slat)
        {
            if (_salt == null)
            {
                _salt = slat;
                _1Remote.Security.Config.SetSalt(slat);
            }
        }
        public static string SimpleEncrypt(string txt)
        {
            return _1Remote.Security.SimpleStringEncipher.Encrypt(txt);
        }
        public static string? SimpleDecrypt(string encryptString)
        {
            return _1Remote.Security.SimpleStringEncipher.Decrypt(encryptString);
        }
    }
}
