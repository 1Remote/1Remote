using System.Runtime.InteropServices;

namespace WindowsShortcutFactory
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct ShellLinkInst
    {
        public unsafe ShellLinkV* Vtbl;
    }
}
