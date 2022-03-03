// Author: NeilHu1994
// original code from https://github.com/NeilHu1994/RemoteDesktopConnection

using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace Shawn.Utils.RdpFile
{
    [Serializable()]
    internal sealed class DataProtection
    {
        [Flags()]
        internal enum CryptProtectPromptFlags
        {
            CRYPTPROTECT_PROMPT_ON_UNPROTECT = 0x01,
            CRYPTPROTECT_PROMPT_ON_PROTECT = 0x02,
            CRYPTPROTECT_PROMPT_RESERVED = 0x04,
            CRYPTPROTECT_PROMPT_STRONG = 0x08,
            CRYPTPROTECT_PROMPT_REQUIRE_STRONG = 0x10
        }

        [Flags()]
        internal enum CryptProtectDataFlags
        {
            CRYPTPROTECT_UI_FORBIDDEN = 0x01,
            CRYPTPROTECT_LOCAL_MACHINE = 0x04,
            CRYPTPROTECT_CRED_SYNC = 0x08,
            CRYPTPROTECT_AUDIT = 0x10,
            CRYPTPROTECT_NO_RECOVERY = 0x20,
            CRYPTPROTECT_VERIFY_PROTECTION = 0x40,
            CRYPTPROTECT_CRED_REGENERATE = 0x80
        }

        internal static string ProtectData(string data, string name)
        {
            return ProtectData(data, name,
                CryptProtectDataFlags.CRYPTPROTECT_UI_FORBIDDEN | CryptProtectDataFlags.CRYPTPROTECT_LOCAL_MACHINE);
        }

        internal static byte[] ProtectData(byte[] data, string name)
        {
            return ProtectData(data, name,
                CryptProtectDataFlags.CRYPTPROTECT_UI_FORBIDDEN | CryptProtectDataFlags.CRYPTPROTECT_LOCAL_MACHINE);
        }

        internal static string ProtectData(string data, string name, CryptProtectDataFlags flags)
        {
            byte[] dataIn = Encoding.Unicode.GetBytes(data);
            byte[] dataOut = ProtectData(dataIn, name, flags);

            if (dataOut != null)
                return (Convert.ToBase64String(dataOut));
            else
                return null;
        }

        internal static byte[] ProtectData(byte[] data, string name, CryptProtectDataFlags dwFlags)
        {
            byte[] cipherText = null;

            // copy data into unmanaged memory
            DPAPI.DATA_BLOB din = new DPAPI.DATA_BLOB();
            din.cbData = data.Length;

            din.pbData = Marshal.AllocHGlobal(din.cbData);

            if (din.pbData.Equals(IntPtr.Zero))
                throw new OutOfMemoryException("Unable to allocate memory for buffer.");

            Marshal.Copy(data, 0, din.pbData, din.cbData);

            DPAPI.DATA_BLOB dout = new DPAPI.DATA_BLOB();

            try
            {
                bool cryptoRetval = DPAPI.CryptProtectData(ref din, name, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, dwFlags, ref dout);

                if (cryptoRetval)
                {
                    int startIndex = 0;
                    cipherText = new byte[dout.cbData];
                    Marshal.Copy(dout.pbData, cipherText, startIndex, dout.cbData);
                    DPAPI.LocalFree(dout.pbData);
                }
                else
                {
                    int errCode = Marshal.GetLastWin32Error();
                    StringBuilder buffer = new StringBuilder(256);
                    Win32Error.FormatMessage(Win32Error.FormatMessageFlags.FORMAT_MESSAGE_FROM_SYSTEM, IntPtr.Zero, errCode, 0, buffer, buffer.Capacity, IntPtr.Zero);
                }
            }
            finally
            {
                if (!din.pbData.Equals(IntPtr.Zero))
                    Marshal.FreeHGlobal(din.pbData);
            }

            return cipherText;
        }

        internal static void InitPromptstruct(ref DPAPI.CRYPTPROTECT_PROMPTSTRUCT ps)
        {
            ps.cbSize = Marshal.SizeOf(typeof(DPAPI.CRYPTPROTECT_PROMPTSTRUCT));
            ps.dwPromptFlags = 0;
            ps.hwndApp = IntPtr.Zero;
            ps.szPrompt = null;
        }
    }

    [SuppressUnmanagedCodeSecurity()]
    internal class DPAPI
    {
        [DllImport("crypt32")]
        public static extern bool CryptProtectData(ref DATA_BLOB dataIn, string szDataDescr, IntPtr optionalEntropy, IntPtr pvReserved,
            IntPtr pPromptStruct, DataProtection.CryptProtectDataFlags dwFlags, ref DATA_BLOB pDataOut);

        [DllImport("crypt32")]
        public static extern bool CryptUnprotectData(ref DATA_BLOB dataIn, StringBuilder ppszDataDescr, IntPtr optionalEntropy,
            IntPtr pvReserved, IntPtr pPromptStruct, DataProtection.CryptProtectDataFlags dwFlags, ref DATA_BLOB pDataOut);

        [DllImport("Kernel32.dll")]
        public static extern IntPtr LocalFree(IntPtr hMem);

        [StructLayout(LayoutKind.Sequential)]
        public struct DATA_BLOB
        {
            public int cbData;
            public IntPtr pbData;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct CRYPTPROTECT_PROMPTSTRUCT
        {
            public int cbSize; // = Marshal.SizeOf(typeof(CRYPTPROTECT_PROMPTSTRUCT))
            public int dwPromptFlags; // = 0
            public IntPtr hwndApp; // = IntPtr.Zero
            public string szPrompt; // = null
        }
    }

    internal class Win32Error
    {
        [Flags()]
        internal enum FormatMessageFlags : int
        {
            FORMAT_MESSAGE_ALLOCATE_BUFFER = 0x0100,
            FORMAT_MESSAGE_IGNORE_INSERTS = 0x0200,
            FORMAT_MESSAGE_FROM_STRING = 0x0400,
            FORMAT_MESSAGE_FROM_HMODULE = 0x0800,
            FORMAT_MESSAGE_FROM_SYSTEM = 0x1000,
            FORMAT_MESSAGE_ARGUMENT_ARRAY = 0x2000,
            FORMAT_MESSAGE_MAX_WIDTH_MASK = 0xFF,
        }

        [DllImport("Kernel32.dll")]
        internal static extern int FormatMessage(FormatMessageFlags flags, IntPtr source, int messageId, int languageId,
            StringBuilder buffer, int size, IntPtr arguments);
    }
}