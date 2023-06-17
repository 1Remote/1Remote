using System.Runtime.InteropServices;

namespace WindowsShortcutFactory
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct PersistFileInst
    {
        public unsafe PersistFileV* Vtbl;
    }
}
