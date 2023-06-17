using System;
using System.Runtime.InteropServices;

namespace WindowsShortcutFactory
{
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct ShellLinkV
    {
        public delegate* unmanaged[Stdcall]<ShellLinkInst*, Guid*, void**, uint> QueryInterface;
        public delegate* unmanaged[Stdcall]<ShellLinkInst*, uint> AddRef;
        public delegate* unmanaged[Stdcall]<ShellLinkInst*, uint> Release;

        //       HRESULT(STDMETHODCALLTYPE* GetPath)(
        //__RPC__in IShellLinkW * This,
        ///* [size_is][string][out] */ __RPC__out_ecount_full_string(cch) LPWSTR pszFile,
        //           /* [in] */ int cch,
        //           /* [unique][out][in] */ __RPC__inout_opt WIN32_FIND_DATAW* pfd,
        //           /* [in] */ DWORD fFlags);
        public delegate* unmanaged[Stdcall]<ShellLinkInst*, byte*, int, void*, uint, uint> GetPath;

        //       HRESULT(STDMETHODCALLTYPE* GetIDList)(
        //        __RPC__in IShellLinkW * This,
        //        /* [out] */ __RPC__deref_out_opt PIDLIST_ABSOLUTE* ppidl);
        public delegate* unmanaged[Stdcall]<ShellLinkInst*, void*, uint> GetIDList;

        //       HRESULT(STDMETHODCALLTYPE* SetIDList)(
        //        __RPC__in IShellLinkW * This,
        //        /* [unique][in] */ __RPC__in_opt PCIDLIST_ABSOLUTE pidl);
        public delegate* unmanaged[Stdcall]<ShellLinkInst*, void*, int, uint> SetIDList;

        //       HRESULT(STDMETHODCALLTYPE* GetDescription)(
        //        __RPC__in IShellLinkW * This,
        //        /* [size_is][string][out] */ __RPC__out_ecount_full_string(cch) LPWSTR pszName,
        //           int cch);
        public delegate* unmanaged[Stdcall]<ShellLinkInst*, byte*, int, uint> GetDescription;

        //       HRESULT(STDMETHODCALLTYPE* SetDescription)(
        //        __RPC__in IShellLinkW * This,
        //        /* [string][in] */ __RPC__in_string LPCWSTR pszName);
        public delegate* unmanaged[Stdcall]<ShellLinkInst*, nint, uint> SetDescription;

        //       HRESULT(STDMETHODCALLTYPE* GetWorkingDirectory)(
        //        __RPC__in IShellLinkW * This,
        //        /* [size_is][string][out] */ __RPC__out_ecount_full_string(cch) LPWSTR pszDir,
        //           int cch);
        public delegate* unmanaged[Stdcall]<ShellLinkInst*, byte*, int, uint> GetWorkingDirectory;

        //       HRESULT(STDMETHODCALLTYPE* SetWorkingDirectory)(
        //        __RPC__in IShellLinkW * This,
        //        /* [string][in] */ __RPC__in_string LPCWSTR pszDir);
        public delegate* unmanaged[Stdcall]<ShellLinkInst*, nint, uint> SetWorkingDirectory;

        //       HRESULT(STDMETHODCALLTYPE* GetArguments)(
        //        __RPC__in IShellLinkW * This,
        //        /* [size_is][string][out] */ __RPC__out_ecount_full_string(cch) LPWSTR pszArgs,
        //           /* [in] */ int cch);
        public delegate* unmanaged[Stdcall]<ShellLinkInst*, byte*, int, uint> GetArguments;

        //       HRESULT(STDMETHODCALLTYPE* SetArguments)(
        //        __RPC__in IShellLinkW * This,
        //        /* [string][in] */ __RPC__in_string LPCWSTR pszArgs);
        public delegate* unmanaged[Stdcall]<ShellLinkInst*, nint, uint> SetArguments;

        //       HRESULT(STDMETHODCALLTYPE* GetHotkey)(
        //        __RPC__in IShellLinkW * This,
        //        /* [out] */ __RPC__out WORD* pwHotkey);
        public delegate* unmanaged[Stdcall]<ShellLinkInst*, ushort*, uint> GetHotKey;

        //       HRESULT(STDMETHODCALLTYPE* SetHotkey)(
        //        __RPC__in IShellLinkW * This,
        //        /* [in] */ WORD wHotkey);
        public delegate* unmanaged[Stdcall]<ShellLinkInst*, ushort, uint> SetHotKey;

        //       HRESULT(STDMETHODCALLTYPE* GetShowCmd)(
        //        __RPC__in IShellLinkW * This,
        //        /* [out] */ __RPC__out int* piShowCmd);
        public delegate* unmanaged[Stdcall]<ShellLinkInst*, int*, uint> GetShowCmd;

        //       HRESULT(STDMETHODCALLTYPE* SetShowCmd)(
        //        __RPC__in IShellLinkW * This,
        //           /* [in] */ int iShowCmd);
        public delegate* unmanaged[Stdcall]<ShellLinkInst*, int, uint> SetShowCmd;

        //       HRESULT(STDMETHODCALLTYPE* GetIconLocation)(
        //        __RPC__in IShellLinkW * This,
        //        /* [size_is][string][out] */ __RPC__out_ecount_full_string(cch) LPWSTR pszIconPath,
        //           /* [in] */ int cch,
        //           /* [out] */ __RPC__out int* piIcon);
        public delegate* unmanaged[Stdcall]<ShellLinkInst*, byte*, int, int*, uint> GetIconLocation;

        //       HRESULT(STDMETHODCALLTYPE* SetIconLocation)(
        //        __RPC__in IShellLinkW * This,
        //        /* [string][in] */ __RPC__in_string LPCWSTR pszIconPath,
        //           /* [in] */ int iIcon);
        public delegate* unmanaged[Stdcall]<ShellLinkInst*, nint, int, uint> SetIconLocation;

        //       HRESULT(STDMETHODCALLTYPE* SetRelativePath)(
        //        __RPC__in IShellLinkW * This,
        //        /* [string][in] */ __RPC__in_string LPCWSTR pszPathRel,
        //        /* [in] */ DWORD dwReserved);
        public delegate* unmanaged[Stdcall]<ShellLinkInst*, byte*, int, uint> SetRelativePath;

        //       HRESULT(STDMETHODCALLTYPE* Resolve)(
        //        __RPC__in IShellLinkW * This,
        //        /* [unique][in] */ __RPC__in_opt HWND hwnd,
        //        /* [in] */ DWORD fFlags);
        public delegate* unmanaged[Stdcall]<ShellLinkInst*, nint, uint, uint> Resolve;

        //       HRESULT(STDMETHODCALLTYPE* SetPath)(
        //        __RPC__in IShellLinkW * This,
        //        /* [string][in] */ __RPC__in_string LPCWSTR pszFile);
        public delegate* unmanaged[Stdcall]<ShellLinkInst*, nint, uint> SetPath;
    }
}
