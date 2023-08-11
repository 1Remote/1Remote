using System.Runtime.InteropServices;

namespace _1RM.Utils.Windows.WindowsShortcutFactory
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct ShellLinkInst
    {
        public unsafe ShellLinkV* Vtbl;
    }
}
