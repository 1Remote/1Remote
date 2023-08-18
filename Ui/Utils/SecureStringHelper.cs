using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace _1RM.Utils
{
    [SuppressUnmanagedCodeSecurity]
    public static class SecureStringHelper
    {
        // Methods
        public static unsafe SecureString CreateSecureString(this string plainString)
        {
            SecureString str;
            if (string.IsNullOrEmpty(plainString))
            {
                return new SecureString();
            }
            fixed (char* str2 = plainString)
            {
                char* chPtr = str2;
                str = new SecureString(chPtr, plainString.Length);
                str.MakeReadOnly();
            }
            return str;
        }

        public static string CreateString(this SecureString secureString)
        {
            string str;
            IntPtr zero = IntPtr.Zero;
            if ((secureString == null) || (secureString.Length == 0))
            {
                return string.Empty;
            }
            try
            {
                zero = Marshal.SecureStringToBSTR(secureString);
                str = Marshal.PtrToStringBSTR(zero);
            }
            finally
            {
                if (zero != IntPtr.Zero)
                {
                    Marshal.ZeroFreeBSTR(zero);
                }
            }
            return str;
        }
    }
}
