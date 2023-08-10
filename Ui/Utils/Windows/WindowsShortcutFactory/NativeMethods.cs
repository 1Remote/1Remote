using System;
using System.Runtime.InteropServices;

namespace _1RM.Utils.Windows.WindowsShortcutFactory
{
    internal static class NativeMethods
    {
        [DllImport("ole32.dll", CallingConvention = CallingConvention.Winapi, SetLastError = false)]
        public static extern unsafe uint CoCreateInstance(Guid* rclsid, void* pUnkOuter, uint dwClsContext, Guid* riid, void** ppv);
    }
}
