using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using static _1RM.Utils.WindowsApi.Credential.Credential.NativeMethods;

namespace _1RM.Utils.WindowsApi.Credential
{
    public class Credential : IDisposable
    {
        /// <summary>
        /// https://learn.microsoft.com/en-us/windows/win32/api/wincred/ns-wincred-credentiala
        /// </summary>
        public enum CredentialTypeEnum : uint
        {
            /// <summary>
            /// 通用证书,不特定于任何认证包
            /// </summary>
            Generic = 1,

            /// <summary>
            /// 密码证书,可由 NTLM、Kerberos、Negotiate 认证包自动使用
            /// </summary>
            DomainPassword = 2,

            /// <summary>
            /// 证书证书,可由 Kerberos、Negotiate、Schannel 认证包自动使用
            /// </summary> 
            DomainCertificate = 3,

            /// <summary>
            /// 不再支持的域可见密码
            /// </summary>
            DomainVisiblePassword = 4,

            /// <summary>
            /// 通用证书证书,适用于通用认证包 
            /// </summary>
            GenericCertificate = 5,

            /// <summary>
            /// 扩展 Negotiate 包支持的证书
            /// </summary>
            DomainExtended = 6,

            /// <summary>
            /// 支持的最大证书类型数 
            /// </summary>
            Maximum = 7,

            /// <summary>
            /// 扩展的最大支持证书类型数,允许新应用在旧系统上运行
            /// </summary>
            MaximumEx = Maximum + 1000
        }

        /// <summary>
        /// https://learn.microsoft.com/en-us/windows/win32/api/wincred/ns-wincred-credentiala
        /// </summary>
        public enum PersistTypeEnum : uint
        {
            /// <summary>
            /// The credential persists for the life of the logon session. It will not be visible to other logon sessions of this same user. It will not exist after this user logs off and back on.
            /// </summary>
            Session = 1,
            /// <summary>
            /// The credential persists for all subsequent logon sessions on this same computer. It is visible to other logon sessions of this same user on this same computer and not visible to logon sessions for this user on other computers.
            /// Windows Vista Home Basic, Windows Vista Home Premium, Windows Vista Starter and Windows XP Home Edition:  This value is not supported.
            /// </summary>
            LocalComputer = 2,
            /// <summary>
            /// The credential persists for all subsequent logon sessions on this same computer. It is visible to other logon sessions of this same user on this same computer and to logon sessions for this user on other computers.
            /// This option can be implemented as locally persisted credential if the administrator or user configures the user account to not have roam-able state. For instance, if the user has no roaming profile, the credential will only persist locally.
            /// Windows Vista Home Basic, Windows Vista Home Premium, Windows Vista Starter and Windows XP Home Edition:  This value is not supported.
            /// </summary>
            Enterprise = 3
        }


        public Credential(string credentialName)
        {
            CredentialName = credentialName;
        }



        public void Dispose()
        {
            SecurePassword?.Clear();
            SecurePassword?.Dispose();

            // Prevent GC Collection since we have already disposed of this object
            GC.SuppressFinalize(this);
        }
        ~Credential()
        {
            Dispose();
        }


        public string Username { get; set; } = "";

        public string Password
        {
            get => SecurePassword?.CreateString() ?? "";
            set => SecurePassword = value.CreateSecureString();
        }

        private SecureString? _password;
        public SecureString? SecurePassword
        {
            get => null == _password ? new SecureString() : _password.Copy();
            set
            {
                if (null != _password)
                {
                    _password.Clear();
                    _password.Dispose();
                }
                _password = null == value ? new SecureString() : value.Copy();
            }
        }

        public readonly string CredentialName;

        public string Description { get; set; } = "";

        public DateTime LastWriteTime => LastWriteTimeUtc.ToLocalTime();

        public DateTime LastWriteTimeUtc { get; private set; }

        public CredentialTypeEnum CredentialType { get; set; } = CredentialTypeEnum.Generic;

        public PersistTypeEnum PersistType { get; set; } = PersistTypeEnum.LocalComputer;

        public bool Save()
        {
            byte[] passwordBytes = Encoding.Unicode.GetBytes(Password);
            if (Password.Length > 512)
            {
                throw new ArgumentOutOfRangeException("The password has exceeded 512 bytes.");
            }

            var credential = new CREDENTIAL
            {
                TargetName = CredentialName,
                UserName = Username,
                CredentialBlob = Marshal.StringToCoTaskMemUni(Password),
                CredentialBlobSize = passwordBytes.Length,
                Comment = Description,
                Type = (int)CredentialType,
                Persist = (int)PersistType
            };

            bool result = CredWrite(ref credential, 0);
            if (!result)
            {
                return false;
            }
            LastWriteTimeUtc = DateTime.UtcNow;
            return true;
        }

        public bool Delete()
        {
            if (string.IsNullOrEmpty(CredentialName))
            {
                throw new InvalidOperationException("CredentialName must be specified to delete a credential.");
            }

            var target = string.IsNullOrEmpty(CredentialName) ? new StringBuilder() : new StringBuilder(CredentialName);
            bool result = CredDelete(target, CredentialType, 0);
            return result;
        }

        public static Credential? Load(string credentialName, CredentialTypeEnum credentialType)
        {
            bool result = CredRead(credentialName, credentialType, 0, out var credPointer);
            if (!result)
            {
                return null;
            }
            using var credentialHandle = new CriticalCredentialHandle(credPointer);
            var ret = new Credential(credentialName);
            var credential = credentialHandle.GetCredential();
            if (credential.CredentialBlobSize > 0)
            {
                ret.Password = Marshal.PtrToStringUni(credential.CredentialBlob, credential.CredentialBlobSize / 2);
            }
            ret.CredentialType = (CredentialTypeEnum)credential.Type;
            ret.PersistType = (PersistTypeEnum)credential.Persist;
            ret.Description = credential.Comment;
            ret.LastWriteTimeUtc = DateTime.FromFileTimeUtc(credential.LastWritten);
            return ret;
        }


        internal static class NativeMethods
        {
            /// <summary>
            /// https://learn.microsoft.com/en-us/windows/win32/api/wincred/ns-wincred-credentialw
            /// </summary>
            [StructLayout(LayoutKind.Sequential)]
            internal struct CREDENTIAL
            {
                public int Flags;
                public int Type;
                [MarshalAs(UnmanagedType.LPWStr)]
                public string TargetName;
                [MarshalAs(UnmanagedType.LPWStr)]
                public string Comment;
                public long LastWritten;
                public int CredentialBlobSize;
                public IntPtr CredentialBlob;
                public int Persist;
                public int AttributeCount;
                public IntPtr Attributes;
                [MarshalAs(UnmanagedType.LPWStr)]
                public string TargetAlias;
                [MarshalAs(UnmanagedType.LPWStr)]
                public string UserName;
            }
            internal sealed class CriticalCredentialHandle : CriticalHandleZeroOrMinusOneIsInvalid
            {
                // Set the handle.
                internal CriticalCredentialHandle(IntPtr preexistingHandle)
                {
                    SetHandle(preexistingHandle);
                }

                internal CREDENTIAL GetCredential()
                {
                    if (!IsInvalid)
                    {
                        // Get the Credential from the mem location
                        return (CREDENTIAL)Marshal.PtrToStructure(handle, typeof(CREDENTIAL));
                    }
                    else
                    {
                        throw new InvalidOperationException("Invalid CriticalHandle!");
                    }
                }

                // Perform any specific actions to release the handle in the ReleaseHandle method.
                // Often, you need to use Pinvoke to make a call into the Win32 API to release the 
                // handle. In this case, however, we can use the Marshal class to release the unmanaged memory.
                protected override bool ReleaseHandle()
                {
                    // If the handle was set, free it. Return success.
                    if (!IsInvalid)
                    {
                        // NOTE: We should also ZERO out the memory allocated to the handle, before free'ing it
                        // so there are no traces of the sensitive data left in memory.
                        CredFree(handle);
                        // Mark the handle as invalid for future users.
                        SetHandleAsInvalid();
                        return true;
                    }
                    // Return false. 
                    return false;
                }
            }

            [DllImport("Advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
            internal static extern bool CredRead(string target, CredentialTypeEnum type, int reservedFlag, out IntPtr CredentialPtr);

            [DllImport("Advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
            internal static extern bool CredWrite([In] ref CREDENTIAL userCredential, [In] uint flags);

            [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode)]
            internal static extern bool CredDelete(StringBuilder target, CredentialTypeEnum type, int flags);

            [DllImport("Advapi32.dll", EntryPoint = "CredFree", SetLastError = true)]
            internal static extern bool CredFree([In] IntPtr cred);
        }
    }
}