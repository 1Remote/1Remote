using System;
using System.Runtime.InteropServices;

namespace WindowsShortcutFactory
{
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct PersistFileV
    {
        public delegate* unmanaged[Stdcall]<PersistFileInst*, Guid*, void**, uint> QueryInterface;
        public delegate* unmanaged[Stdcall]<PersistFileInst*, uint> AddRef;
        public delegate* unmanaged[Stdcall]<PersistFileInst*, uint> Release;

        public delegate* unmanaged[Stdcall]<PersistFileInst*, void**, uint> GetClassID;

        public delegate* unmanaged[Stdcall]<PersistFileInst*, uint> IsDirty;
        public delegate* unmanaged[Stdcall]<PersistFileInst*, nint, uint, uint> Load;
        public delegate* unmanaged[Stdcall]<PersistFileInst*, nint, uint, uint> Save;
        public delegate* unmanaged[Stdcall]<PersistFileInst*, void*, uint> SaveCompleted;
        public delegate* unmanaged[Stdcall]<PersistFileInst*, void*, uint> GetCurFile;
    }
}
