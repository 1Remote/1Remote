using System.Runtime.InteropServices;

namespace _1RM.Utils.Windows.WindowsShortcutFactory
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct PersistFileInst
    {
        public unsafe PersistFileV* Vtbl;
    }
}
