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
            var ret = _1Remote.Security.SimpleStringEncipher.Decrypt(encryptString);
            if(ret.IsSuccess)
                return ret.PlainText;
            return null;
        }

        public static string EncryptOnce(string str)
        {
            if (SimpleDecrypt(str) == null)
                return SimpleEncrypt(str);
            return str;
        }
        public static string DecryptOrReturnOriginalString(string originalString)
        {
            return SimpleDecrypt(originalString) ?? originalString;
        }
    }
}
