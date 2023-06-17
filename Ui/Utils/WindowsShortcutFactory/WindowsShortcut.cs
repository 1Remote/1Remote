using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace WindowsShortcutFactory
{
    /// <summary>
    /// Represents a Windows shortcut.
    /// </summary>
    public sealed class WindowsShortcut : IDisposable
    {
        private const uint CLSCTX_INPROC_SERVER = 0x1;
        private const uint SLGP_RAWPATH = 4;
        private static readonly Guid CLSID_ShellLink = new("00021401-0000-0000-C000-000000000046");
        private static readonly Guid IID_IShellLinkW = new(0x000214F9, 0, 0, 0xC0, 0, 0, 0, 0, 0, 0, 0x46);
        private static readonly Guid IID_IPersistFile = new("0000010b-0000-0000-C000-000000000046");
        private readonly unsafe ShellLinkInst* inst;
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowsShortcut"/> class.
        /// </summary>
        public WindowsShortcut()
        {
            unsafe
            {
                ShellLinkInst* inst = null;
                var clsid = CLSID_ShellLink;
                var iid = IID_IShellLinkW;
                uint res = NativeMethods.CoCreateInstance(&clsid, null, CLSCTX_INPROC_SERVER, &iid, (void**)&inst);
                if (res != 0)
                    throw new COMException("Unable to create ShellLink object.", (int)res);

                this.inst = inst;
            }
        }
        private unsafe WindowsShortcut(ShellLinkInst* inst) => this.inst = inst;
        ~WindowsShortcut() => this.Dispose(false);

        /// <summary>
        /// Gets or sets the arguments to pass to the target.
        /// </summary>
        public string? Arguments
        {
            get
            {
                unsafe
                {
                    return this.GetString(this.inst->Vtbl->GetArguments);
                }
            }
            set
            {
                unsafe
                {
                    this.SetString(this.inst->Vtbl->SetArguments, value);
                }
            }
        }
        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        public string? Description
        {
            get
            {
                unsafe
                {
                    return this.GetString(this.inst->Vtbl->GetDescription);
                }
            }
            set
            {
                unsafe
                {
                    this.SetString(this.inst->Vtbl->SetDescription, value);
                }
            }
        }
        /// <summary>
        /// Gets or sets the path of the target.
        /// </summary>
        public string? Path
        {
            get
            {
                unsafe
                {
                    var buffer = stackalloc char[260];
                    uint res = this.inst->Vtbl->GetPath(this.inst, (byte*)buffer, 260, null, SLGP_RAWPATH);
                    if (res == 0)
                        return Marshal.PtrToStringUni(new IntPtr(buffer));
                    else
                        return null;
                }
            }
            set
            {
                unsafe
                {
                    this.SetString(this.inst->Vtbl->SetPath, value);
                }
            }
        }
        /// <summary>
        /// Gets or sets the working directory to set.
        /// </summary>
        public string? WorkingDirectory
        {
            get
            {
                unsafe
                {
                    return this.GetString(this.inst->Vtbl->GetWorkingDirectory);
                }
            }
            set
            {
                unsafe
                {
                    this.SetString(this.inst->Vtbl->SetWorkingDirectory, value);
                }
            }
        }
        /// <summary>
        /// Gets or sets the location of the icon to use for the shortcut.
        /// </summary>
        public IconLocation IconLocation
        {
            get
            {
                unsafe
                {
                    int index = 0;
                    var buffer = stackalloc char[260];
                    uint res = this.inst->Vtbl->GetIconLocation(this.inst, (byte*)buffer, 260, &index);
                    if (res == 0)
                    {
                        var path = Marshal.PtrToStringUni(new IntPtr(buffer));
                        return path != null ? new IconLocation(path, index) : default;
                    }
                    else
                    {
                        return default;
                    }
                }
            }
            set
            {
                unsafe
                {
                    if (value.IsValid)
                    {
                        var ptr = Marshal.StringToCoTaskMemUni(value.Path);
                        try
                        {
                            this.inst->Vtbl->SetIconLocation(this.inst, ptr, value.Index);
                        }
                        finally
                        {
                            Marshal.FreeCoTaskMem(ptr);
                        }
                    }
                    else
                    {
                        this.inst->Vtbl->SetIconLocation(this.inst, default, 0);
                    }
                }
            }
        }
        /// <summary>
        /// Gets or sets the window show command.
        /// </summary>
        public ProcessWindowStyle ShowCommand
        {
            get
            {
                unsafe
                {
                    int value = 0;
                    this.inst->Vtbl->GetShowCmd(this.inst, &value);
                    return value switch
                    {
                        2 => ProcessWindowStyle.Minimized,
                        3 => ProcessWindowStyle.Maximized,
                        _ => ProcessWindowStyle.Normal
                    };
                }
            }
            set
            {
                int n = value switch
                {
                    ProcessWindowStyle.Normal => 1,
                    ProcessWindowStyle.Minimized => 2,
                    ProcessWindowStyle.Maximized => 3,
                    _ => throw new ArgumentException("Expected Normal, Minimized, or Maximized.")
                };

                unsafe
                {
                    uint res = this.inst->Vtbl->SetShowCmd(this.inst, n);
                    if (res != 0)
                        throw new COMException("Unable to set window show command.", (int)res);
                }
            }
        }

        /// <summary>
        /// Loads a shortcut file from disk.
        /// </summary>
        /// <param name="fileName">Full path of the shortcut file to load.</param>
        /// <returns>Instance of <see cref="WindowsShortcut"/> that represents the specified shortcut file.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="fileName"/> is null or empty.</exception>
        /// <exception cref="FileNotFoundException">The shortcut file was not found.</exception>
        public static WindowsShortcut Load(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentNullException(nameof(fileName));
            if (!File.Exists(fileName))
                throw new FileNotFoundException();

            var shortcut = new WindowsShortcut();
            try
            {
                shortcut.LoadInternal(fileName);
                return shortcut;
            }
            catch
            {
                shortcut.Dispose();
                throw;
            }
        }
        /// <summary>
        /// Write the shortcut to a file on disk.
        /// </summary>
        /// <param name="fileName">Full path of the shortcut file to write.</param>
        /// <remarks>Shortcuts normally have a .lnk file extension.</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="fileName"/> is null or empty.</exception>
        /// <exception cref="DirectoryNotFoundException">The directory specified in <paramref name="fileName"/> does not exit.</exception>
        public void Save(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentNullException(nameof(fileName));
            if (System.IO.Path.IsPathRooted(fileName) && !Directory.Exists(System.IO.Path.GetDirectoryName(fileName)))
                throw new DirectoryNotFoundException();

            unsafe
            {
                var iid = IID_IPersistFile;
                PersistFileInst* persistInst = null;

                uint res = inst->Vtbl->QueryInterface(this.inst, &iid, (void**)&persistInst);
                if (res != 0)
                    throw new COMException("Unable to get IPersistFile interface.", (int)res);

                try
                {
                    var ptr = Marshal.StringToCoTaskMemUni(fileName);
                    try
                    {
                        res = persistInst->Vtbl->Save(persistInst, ptr, (uint)fileName.Length);
                        if (res != 0)
                            throw new COMException("Unable to save shortcut file.", (int)res);
                    }
                    finally
                    {
                        Marshal.FreeCoTaskMem(ptr);
                    }
                }
                finally
                {
                    persistInst->Vtbl->Release(persistInst);
                }
            }
        }
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                unsafe
                {
                    this.inst->Vtbl->Release(this.inst);
                }

                this.disposed = true;
            }
        }
        private void LoadInternal(string fileName)
        {
            unsafe
            {
                var iid = IID_IPersistFile;
                PersistFileInst* persistInst = null;

                uint res = inst->Vtbl->QueryInterface(this.inst, &iid, (void**)&persistInst);
                if (res != 0)
                    throw new COMException("Unable to get IPersistFile interface.", (int)res);

                try
                {
                    var ptr = Marshal.StringToCoTaskMemUni(fileName);
                    try
                    {
                        res = persistInst->Vtbl->Load(persistInst, ptr, 0);
                        if (res != 0)
                            throw new COMException("Unable to load shortcut file.", (int)res);
                    }
                    finally
                    {
                        Marshal.FreeCoTaskMem(ptr);
                    }
                }
                finally
                {
                    persistInst->Vtbl->Release(persistInst);
                }
            }
        }
        private unsafe string? GetString(delegate* unmanaged[Stdcall]<ShellLinkInst*, byte*, int, uint> getMethod)
        {
            var buffer = stackalloc char[260];
            uint res = getMethod(this.inst, (byte*)buffer, 260);
            if (res == 0)
                return Marshal.PtrToStringUni(new IntPtr(buffer));
            else
                return null;
        }
        private unsafe void SetString(delegate* unmanaged[Stdcall]<ShellLinkInst*, nint, uint> setMethod, string? value)
        {
            if (value != null)
            {
                var ptr = Marshal.StringToCoTaskMemUni(value);
                try
                {
                    uint res = setMethod(this.inst, ptr);
                }
                finally
                {
                    Marshal.FreeCoTaskMem(ptr);
                }
            }
            else
            {
                setMethod(this.inst, 0);
            }
        }
    }
}
