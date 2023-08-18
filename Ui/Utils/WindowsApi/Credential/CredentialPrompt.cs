using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace _1RM.Utils.WindowsApi.Credential
{
    public static class CredentialPrompt
    {
        #region IPromptCredentialsResult
        public interface IPromptCredentialsResult
        {
            public bool IsSaveChecked { get; set; }
            public int? ErrorCode { get; set; }
            public string? ErrorMessage { get; set; }
        }
        public class PromptCredentialsResult : IPromptCredentialsResult
        {
            public static PromptCredentialsResult Error(int errorCode, string errorMessage)
            {
                return new PromptCredentialsResult("", "", "", false, errorCode, errorMessage);
            }
            public PromptCredentialsResult(string userName, string domainName, string password, bool isSaveChecked, int? errorCode = null, string? errorMessage = null)
            {
                UserName = userName;
                DomainName = domainName;
                Password = password;
                IsSaveChecked = isSaveChecked;
                ErrorCode = errorCode;
                ErrorMessage = errorMessage;
            }

            public string UserName { get; internal set; }
            public string DomainName { get; internal set; }
            public string Password { get; internal set; }
            public bool IsSaveChecked { get; set; }
            public int? ErrorCode { get; set; }
            public string? ErrorMessage { get; set; }

            public bool HasNoError => ErrorCode == null;
        }
        public class PromptCredentialsSecureStringResult : IPromptCredentialsResult
        {
            public static PromptCredentialsSecureStringResult Error(int errorCode, string errorMessage)
            {
                return new PromptCredentialsSecureStringResult(new SecureString(), new SecureString(), new SecureString(), false, errorCode, errorMessage);
            }
            public PromptCredentialsSecureStringResult(SecureString userName, SecureString domainName, SecureString password, bool isSaveChecked, int? errorCode = null, string? errorMessage = null)
            {
                UserName = userName;
                DomainName = domainName;
                Password = password;
                IsSaveChecked = isSaveChecked;
                ErrorCode = errorCode;
            }

            public SecureString UserName { get; internal set; }
            public SecureString DomainName { get; internal set; }
            public SecureString Password { get; internal set; }
            public bool IsSaveChecked { get; set; }
            public int? ErrorCode { get; set; }
            public string? ErrorMessage { get; set; }
        }
        #endregion

        [Flags]
        public enum PromptForWindowsCredentialsFlag : uint
        {
            CREDUIWIN_NONE = 0x00000000,
            /// <summary>
            /// Plain text username/password is being requested
            /// </summary>
            CREDUIWIN_GENERIC = 0x00000001,
            /// <summary>
            /// Show the Save Credential checkbox
            /// </summary>
            CREDUIWIN_CHECKBOX = 0x00000002,
            /// <summary>
            /// Only Cred Providers that support the input auth package should enumerate
            /// </summary>
            CREDUIWIN_AUTHPACKAGE_ONLY = 0x00000010,
            /// <summary>
            /// Only the incoming cred for the specific auth package should be enumerated
            /// </summary>
            CREDUIWIN_IN_CRED_ONLY = 0x00000020,
            /// <summary>
            /// Cred Providers should enumerate administrators only
            /// </summary>
            CREDUIWIN_ENUMERATE_ADMINS = 0x00000100,
            /// <summary>
            /// Only the incoming cred for the specific auth package should be enumerated
            /// </summary>
            CREDUIWIN_ENUMERATE_CURRENT_USER = 0x00000200,
            /// <summary>
            /// The Credui prompt should be displayed on the secure desktop
            /// </summary>
            CREDUIWIN_SECURE_PROMPT = 0x00001000,
            /// <summary>
            /// Tell the credential provider it should be packing its Auth Blob 32 bit even though it is running 64 native
            /// </summary>
            CREDUIWIN_PACK_32_WOW = 0x10000000,
            CREDUI_FLAGS_KEEP_USERNAME = 0x100000
        }



        private static T GetPromptForWindowsCredentialsError<T>(int errorCode, string errorMessage) where T : class, IPromptCredentialsResult
        {
            if (typeof(T) == typeof(PromptCredentialsSecureStringResult))
            {
                return (PromptCredentialsSecureStringResult.Error(errorCode, errorMessage) as T)!;
            }
            else
            {
                return (PromptCredentialsResult.Error(errorCode, errorMessage) as T)!;
            }
        }

        private static T? PromptForWindowsCredentialsInternal<T>(
            string caption, string message, IntPtr? hwndParent = null,
            uint credentialsFlag = (uint)PromptForWindowsCredentialsFlag.CREDUIWIN_GENERIC,
            bool isSaveChecked = false,
            string? userName = null, string? password = null) where T : class, IPromptCredentialsResult
        {
            if (caption.Length > NativeMethods.CREDUI_MAX_CAPTION_LENGTH)
                caption = caption.Substring(0, NativeMethods.CREDUI_MAX_CAPTION_LENGTH);
            if (message.Length > NativeMethods.CREDUI_MAX_MESSAGE_LENGTH)
                message = message.Substring(0, NativeMethods.CREDUI_MAX_MESSAGE_LENGTH);
            var credulityInfo = new NativeMethods.CREDUI_INFO()
            {
                pszCaptionText = caption,
                pszMessageText = message,
                hwndParent = hwndParent ?? IntPtr.Zero,
            };

            var userNamePtr = IntPtr.Zero;
            var passwordPtr = IntPtr.Zero;
            var authPackage = 0;
            var outAuthBuffer = IntPtr.Zero;
            var inAuthBuffer = IntPtr.Zero;
            var inAuthBufferSize = 0;
            var save = isSaveChecked;
            using var userNameS = new SecureString();
            using var passwordS = new SecureString();
            try
            {
                if (userName != null || password != null)
                {
                    if (!string.IsNullOrEmpty(userName))
                    {
                        if (userName.Length > NativeMethods.CREDUI_MAX_USERNAME_LENGTH)
                        {
                            return GetPromptForWindowsCredentialsError<T>(-1, "UserName is too long!");
                        }
                        foreach (var c in userName)
                            userNameS.AppendChar(c);
                    }
                    if (!string.IsNullOrEmpty(password))
                    {
                        if (password.Length > NativeMethods.CREDUI_MAX_USERNAME_LENGTH)
                        {
                            return GetPromptForWindowsCredentialsError<T>(-1, "password is too long!");
                        }
                        foreach (var c in password)
                            passwordS.AppendChar(c);
                    }
                    userNamePtr = Marshal.SecureStringToCoTaskMemUnicode(userNameS);
                    passwordPtr = Marshal.SecureStringToCoTaskMemUnicode(passwordS);
                }

                // pre-filled with UserName or Password
                if (userNamePtr != IntPtr.Zero || passwordPtr != IntPtr.Zero)
                {
                    inAuthBufferSize = 1024;
                    inAuthBuffer = Marshal.AllocCoTaskMem(inAuthBufferSize);
                    if (!NativeMethods.CredPackAuthenticationBuffer(0x00, userNamePtr, passwordPtr, inAuthBuffer, ref inAuthBufferSize))
                    {
                        var win32Error = Marshal.GetLastWin32Error();
                        if (win32Error == 122 /*ERROR_INSUFFICIENT_BUFFER*/)
                        {
                            inAuthBuffer = Marshal.ReAllocCoTaskMem(inAuthBuffer, inAuthBufferSize);
                            if (!NativeMethods.CredPackAuthenticationBuffer(0x00, userNamePtr, passwordPtr, inAuthBuffer, ref inAuthBufferSize))
                            {
                                return GetPromptForWindowsCredentialsError<T>(Marshal.GetLastWin32Error(), $"CredPackAuthenticationBuffer error code: {Marshal.GetLastWin32Error()}");
                            }
                        }
                        else
                        {
                            return GetPromptForWindowsCredentialsError<T>(win32Error, $"CredPackAuthenticationBuffer error code: {win32Error}");
                        }
                    }
                }

                var retVal = NativeMethods.CredUIPromptForWindowsCredentials(credulityInfo,
                                                                     0,
                                                                     ref authPackage,
                                                                     inAuthBuffer,
                                                                     inAuthBufferSize,
                                                                     out outAuthBuffer,
                                                                     out var outAuthBufferSize,
                                                                     ref save,
                                                                     //(int)(PromptForWindowsCredentialsFlag.CREDUIWIN_AUTHPACKAGE_ONLY)
                                                                     credentialsFlag // Use the PromptForWindowsCredentialsFlags Enum here. You can use multiple flags if you seperate them with | .
                                                                     );

                switch (retVal)
                {
                    case NativeMethods.CredUIPromptReturnCode.Cancelled:
                        return null;
                    case NativeMethods.CredUIPromptReturnCode.Success:
                        break;
                    default:
                        return GetPromptForWindowsCredentialsError<T>((int)retVal, $"CredUIPromptReturnCode error code: {(int)retVal}");
                        //throw new Win32Exception((Int32)retVal);
                }


                if (typeof(T) == typeof(PromptCredentialsSecureStringResult))
                {
                    var credResult = NativeMethods.CredUnPackAuthenticationBufferWrapSecureString(true, outAuthBuffer, outAuthBufferSize);
                    credResult.IsSaveChecked = save;
                    return credResult as T;
                }
                else
                {
                    var credResult = NativeMethods.CredUnPackAuthenticationBufferWrap(true, outAuthBuffer, outAuthBufferSize);
                    credResult.IsSaveChecked = save;
                    return credResult as T;
                }
            }
            finally
            {
                if (inAuthBuffer != IntPtr.Zero)
                    Marshal.ZeroFreeCoTaskMemUnicode(inAuthBuffer);
                if (outAuthBuffer != IntPtr.Zero)
                    Marshal.ZeroFreeCoTaskMemUnicode(outAuthBuffer);
                if (userNamePtr != IntPtr.Zero)
                    Marshal.ZeroFreeCoTaskMemUnicode(userNamePtr);
                if (passwordPtr != IntPtr.Zero)
                    Marshal.ZeroFreeCoTaskMemUnicode(passwordPtr);
            }
        }

        /// <summary>
        /// Creates and displays a configurable dialog box that allows users to supply credential information by using any credential provider installed on the local computer.
        /// </summary>
        /// <example>
        /// For example:
        /// <code>
        /// var result = CredentialPrompt.PromptForWindowsCredentials("Hi", "body", new WindowInteropHelper(this).Handle);
        /// if (result?.HasNoError == true)
        /// {
        ///     MessageBox.Show($"输入的信息：\r\n  {result.UserName} \r\n {result.Password} \r\n {result.DomainName} \r\n {result.IsSaveChecked}");
        /// }
        /// else
        /// {
        ///     MessageBox.Show("密码未输入完成");
        /// }
        /// </code>
        /// </example>
        /// <returns>return null for cancel or other error</returns>
        public static PromptCredentialsResult? PromptForWindowsCredentials(string caption, string message, IntPtr? hwndParent = null, string? userName = null, string? password = null, uint credentialsFlag = (uint)PromptForWindowsCredentialsFlag.CREDUIWIN_GENERIC)
        {
            return PromptForWindowsCredentialsInternal<PromptCredentialsResult>(caption, message, hwndParent, userName: userName, password: password, credentialsFlag: credentialsFlag);
        }


        /// <summary>
        /// Creates and displays a configurable dialog box that allows users to supply credential information by using any credential provider installed on the local computer.
        /// </summary>
        /// <returns>return null for cancel or other error</returns>
        public static PromptCredentialsSecureStringResult? PromptForWindowsCredentialsSecureString(string caption, string message, IntPtr? hwndParent = null, string? userName = null, string? password = null, uint credentialsFlag = (uint)PromptForWindowsCredentialsFlag.CREDUIWIN_GENERIC)
        {
            return PromptForWindowsCredentialsInternal<PromptCredentialsSecureStringResult>(caption, message, hwndParent, userName: userName, password: password, credentialsFlag: credentialsFlag);
        }

        public enum LogonUserStatus
        {
            Success,
            Cancel,
            Failed,
        }
        public static LogonUserStatus LogonUser(string userName, string userPassword, string domain = "")
        {
            // Call LogonUser to obtain a handle to an access token. 
            bool returnValue = false;
            NativeMethods.SafeTokenHandle? tokenHandle = null;
            if (userName.IndexOf("\\") > 0)
            {
                userName = userName.Substring(userName.IndexOf("\\") + 1);
                //var userName2 = userName.Substring(userName.IndexOf("\\") + 1);
                //returnValue = NativeMethods.LogonUser(userName2, domain, userPassword,
                //    NativeMethods.LogonTypes.LOGON32_LOGON_INTERACTIVE, NativeMethods.LogonProvider.LOGON32_PROVIDER_DEFAULT,
                //    out tokenHandle);
            }

            if (!returnValue)
            {
                returnValue = NativeMethods.LogonUser(userName, domain, userPassword,
                    NativeMethods.LogonTypes.LOGON32_LOGON_INTERACTIVE, NativeMethods.LogonProvider.LOGON32_PROVIDER_DEFAULT,
                    out tokenHandle);
            }

            if (returnValue)
            {
                tokenHandle?.Dispose();
                return LogonUserStatus.Success;
            }
            //else
            //{
            //    int err = Marshal.GetLastWin32Error();
            //    MessageBox.Show(err.ToString("X"));
            //}
            return LogonUserStatus.Failed;
        }

        public static LogonUserStatus LogonUserWithWindowsCredential(string caption, string message, IntPtr? hwndParent = null, string? userName = null, string? password = null, uint credentialsFlag = (int)PromptForWindowsCredentialsFlag.CREDUIWIN_GENERIC)
        {
            userName ??= GetUserName();
            var ret = PromptForWindowsCredentials(caption, message, hwndParent, userName, password, credentialsFlag);
            if (ret?.HasNoError == true)
            {
                return LogonUser(ret.UserName, ret.Password, ret.DomainName);
            }
            else if (ret == null)
            {
                return LogonUserStatus.Cancel;
            }
            else
            {
                //MessageBox.Show("Error code = " + ret.ErrorCode);
            }
            return LogonUserStatus.Failed;
        }


        public static string GetUserName()
        {
            //return System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            return Environment.UserName;
            //return Environment.UserDomainName + "\\" + Environment.UserName;
        }




        private static class NativeMethods
        {
            #region CredUIPromptForWindowsCredentials
            public const int CREDUI_MAX_MESSAGE_LENGTH = 32767;
            public const int CREDUI_MAX_CAPTION_LENGTH = 128;
            public const int CRED_MAX_USERNAME_LENGTH = 256 + 1 + 256;
            public const int CREDUI_MAX_USERNAME_LENGTH = CRED_MAX_USERNAME_LENGTH;
            public const int CREDUI_MAX_PASSWORD_LENGTH = 512 / 2;

            public enum CredUIPromptReturnCode
            {
                Success = 0,
                Cancelled = 1223,
                InvalidParameter = 87,
                InvalidFlags = 1004
            }

            [StructLayout(LayoutKind.Sequential)]
            public class CREDUI_INFO
            {
                public int cbSize;
                public IntPtr hwndParent;
                [MarshalAs(UnmanagedType.LPWStr)]
                public string pszMessageText;
                [MarshalAs(UnmanagedType.LPWStr)]
                public string pszCaptionText;
                public IntPtr hbmBanner;

                public CREDUI_INFO()
                {
                    cbSize = Marshal.SizeOf(typeof(CREDUI_INFO));
                }
            }

            //
            // CredUIPromptForWindowsCredentials ------------------------------
            //
            [DllImport("credui.dll", CharSet = CharSet.Unicode)]
            public static extern CredUIPromptReturnCode
            CredUIPromptForWindowsCredentials(
                CREDUI_INFO pUiInfo,
                int dwAuthError,
                ref int pulAuthPackage,
                IntPtr pvInAuthBuffer,
                int ulInAuthBufferSize,
                out IntPtr ppvOutAuthBuffer,
                out int pulOutAuthBufferSize,
                ref bool pfSave,
                uint dwFlags
            );

            [DllImport("credui.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern bool CredPackAuthenticationBuffer(
                int dwFlags,
                string pszUserName,
                string pszPassword,
                IntPtr pPackedCredentials,
                ref int pcbPackedCredentials
            );
            [DllImport("credui.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern bool CredPackAuthenticationBuffer(
                int dwFlags,
                IntPtr pszUserName,
                IntPtr pszPassword,
                IntPtr pPackedCredentials,
                ref int pcbPackedCredentials
            );

            [DllImport("credui.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern bool CredUnPackAuthenticationBuffer(
                int dwFlags,
                IntPtr pAuthBuffer,
                int cbAuthBuffer,
                StringBuilder pszUserName,
                ref int pcchMaxUserName,
                StringBuilder pszDomainName,
                ref int pcchMaxDomainame,
                StringBuilder pszPassword,
                ref int pcchMaxPassword
            );
            [DllImport("credui.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern bool CredUnPackAuthenticationBufferA(
                int dwFlags,
                IntPtr pAuthBuffer,
                int cbAuthBuffer,
                StringBuilder pszUserName,
                ref int pcchMaxUserName,
                StringBuilder pszDomainName,
                ref int pcchMaxDomainame,
                StringBuilder pszPassword,
                ref int pcchMaxPassword
            );
            [DllImport("credui.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern bool CredUnPackAuthenticationBuffer(
                int dwFlags,
                IntPtr pAuthBuffer,
                int cbAuthBuffer,
                IntPtr pszUserName,
                ref int pcchMaxUserName,
                IntPtr pszDomainName,
                ref int pcchMaxDomainame,
                IntPtr pszPassword,
                ref int pcchMaxPassword
            );



            public static PromptCredentialsResult CredUnPackAuthenticationBufferWrap(bool decryptProtectedCredentials, IntPtr authBufferPtr, int authBufferSize)
            {
                var sbUserName = new StringBuilder(4096);
                var sbDomainName = new StringBuilder(4096);
                var sbPassword = new StringBuilder(4096);
                var userNameSize = sbUserName.Capacity;
                var domainNameSize = sbDomainName.Capacity;
                var passwordSize = sbPassword.Capacity;

                //#define CRED_PACK_PROTECTED_CREDENTIALS      0x1
                //#define CRED_PACK_WOW_BUFFER                 0x2
                //#define CRED_PACK_GENERIC_CREDENTIALS        0x4

                var result = CredUnPackAuthenticationBuffer(decryptProtectedCredentials ? 0x1 : 0x0,
                                                                authBufferPtr,
                                                                authBufferSize,
                                                                sbUserName,
                                                                ref userNameSize,
                                                                sbDomainName,
                                                                ref domainNameSize,
                                                                sbPassword,
                                                                ref passwordSize
                                                                );
                if (!result)
                {
                    var win32Error = Marshal.GetLastWin32Error();
                    if (win32Error == 122 /*ERROR_INSUFFICIENT_BUFFER*/)
                    {
                        sbUserName.Capacity = userNameSize;
                        sbPassword.Capacity = passwordSize;
                        sbDomainName.Capacity = domainNameSize;
                        result = CredUnPackAuthenticationBuffer(decryptProtectedCredentials ? 0x1 : 0x0,
                                                                authBufferPtr,
                                                                authBufferSize,
                                                                sbUserName,
                                                                ref userNameSize,
                                                                sbDomainName,
                                                                ref domainNameSize,
                                                                sbPassword,
                                                                ref passwordSize
                                                                );
                        if (!result)
                        {
                            return PromptCredentialsResult.Error(Marshal.GetLastWin32Error(), "");
                        }
                    }
                    else
                    {
                        return PromptCredentialsResult.Error(win32Error, "");
                    }
                }

                return new PromptCredentialsResult(sbUserName.ToString(), sbDomainName.ToString(), sbPassword.ToString(), false);
            }

            public static PromptCredentialsSecureStringResult CredUnPackAuthenticationBufferWrapSecureString(bool decryptProtectedCredentials, IntPtr authBufferPtr, int authBufferSize)
            {
                var userNameSize = 255;
                var domainNameSize = 255;
                var passwordSize = 255;
                var userNamePtr = IntPtr.Zero;
                var domainNamePtr = IntPtr.Zero;
                var passwordPtr = IntPtr.Zero;
                try
                {
                    userNamePtr = Marshal.AllocCoTaskMem(userNameSize);
                    domainNamePtr = Marshal.AllocCoTaskMem(domainNameSize);
                    passwordPtr = Marshal.AllocCoTaskMem(passwordSize);

                    //#define CRED_PACK_PROTECTED_CREDENTIALS      0x1
                    //#define CRED_PACK_WOW_BUFFER                 0x2
                    //#define CRED_PACK_GENERIC_CREDENTIALS        0x4

                    var result = CredUnPackAuthenticationBuffer(decryptProtectedCredentials ? 0x1 : 0x0,
                                                                    authBufferPtr,
                                                                    authBufferSize,
                                                                    userNamePtr,
                                                                    ref userNameSize,
                                                                    domainNamePtr,
                                                                    ref domainNameSize,
                                                                    passwordPtr,
                                                                    ref passwordSize
                                                                    );
                    if (!result)
                    {
                        var win32Error = Marshal.GetLastWin32Error();
                        if (win32Error == 122 /*ERROR_INSUFFICIENT_BUFFER*/)
                        {
                            userNamePtr = Marshal.ReAllocCoTaskMem(userNamePtr, userNameSize);
                            domainNamePtr = Marshal.ReAllocCoTaskMem(domainNamePtr, domainNameSize);
                            passwordPtr = Marshal.ReAllocCoTaskMem(passwordPtr, passwordSize);
                            result = CredUnPackAuthenticationBuffer(decryptProtectedCredentials ? 0x1 : 0x0,
                                                                    authBufferPtr,
                                                                    authBufferSize,
                                                                    userNamePtr,
                                                                    ref userNameSize,
                                                                    domainNamePtr,
                                                                    ref domainNameSize,
                                                                    passwordPtr,
                                                                    ref passwordSize);
                            if (!result)
                            {
                                throw new Win32Exception(Marshal.GetLastWin32Error());
                            }
                        }
                        else
                        {
                            throw new Win32Exception(win32Error);
                        }
                    }

                    return new PromptCredentialsSecureStringResult
                    (
                        PtrToSecureString(userNamePtr, userNameSize),
                         PtrToSecureString(domainNamePtr, domainNameSize),
                        PtrToSecureString(passwordPtr, passwordSize),
                        false
                    );
                }
                finally
                {
                    if (userNamePtr != IntPtr.Zero)
                        Marshal.ZeroFreeCoTaskMemUnicode(userNamePtr);
                    if (domainNamePtr != IntPtr.Zero)
                        Marshal.ZeroFreeCoTaskMemUnicode(domainNamePtr);
                    if (passwordPtr != IntPtr.Zero)
                        Marshal.ZeroFreeCoTaskMemUnicode(passwordPtr);
                }
            }

            #endregion

            #region LogonUser


            public enum LogonTypes : int
            {
                /// <summary>
                /// This logon type is intended for users who will be interactively using the computer, such as a user being logged on  
                /// by a terminal server, remote shell, or similar process.
                /// This logon type has the additional expense of caching logon information for disconnected operations; 
                /// therefore, it is inappropriate for some client/server applications,
                /// such as a mail server.
                /// </summary>
                LOGON32_LOGON_INTERACTIVE = 2,

                /// <summary>
                /// This logon type is intended for high performance servers to authenticate plaintext passwords.
                /// The LogonUser function does not cache credentials for this logon type.
                /// </summary>
                LOGON32_LOGON_NETWORK = 3,

                /// <summary>
                /// This logon type is intended for batch servers, where processes may be executing on behalf of a user without 
                /// their direct intervention. This type is also for higher performance servers that process many plaintext
                /// authentication attempts at a time, such as mail or Web servers. 
                /// The LogonUser function does not cache credentials for this logon type.
                /// </summary>
                LOGON32_LOGON_BATCH = 4,

                /// <summary>
                /// Indicates a service-type logon. The account provided must have the service privilege enabled. 
                /// </summary>
                LOGON32_LOGON_SERVICE = 5,

                /// <summary>
                /// This logon type is for GINA DLLs that log on users who will be interactively using the computer. 
                /// This logon type can generate a unique audit record that shows when the workstation was unlocked. 
                /// </summary>
                LOGON32_LOGON_UNLOCK = 7,

                /// <summary>
                /// This logon type preserves the name and password in the authentication package, which allows the server to make 
                /// connections to other network servers while impersonating the client. A server can accept plaintext credentials 
                /// from a client, call LogonUser, verify that the user can access the system across the network, and still 
                /// communicate with other servers.
                /// NOTE: Windows NT:  This value is not supported. 
                /// </summary>
                LOGON32_LOGON_NETWORK_CLEARTEXT = 8,

                /// <summary>
                /// This logon type allows the caller to clone its current token and specify new credentials for outbound connections.
                /// The new logon session has the same local identifier but uses different credentials for other network connections. 
                /// NOTE: This logon type is supported only by the LOGON32_PROVIDER_WINNT50 logon provider.
                /// NOTE: Windows NT:  This value is not supported. 
                /// </summary>
                LOGON32_LOGON_NEW_CREDENTIALS = 9,
            }

            public enum LogonProvider : int
            {
                /// <summary>
                /// Use the standard logon provider for the system. 
                /// The default security provider is negotiate, unless you pass NULL for the domain name and the user name 
                /// is not in UPN format. In this case, the default provider is NTLM. 
                /// NOTE: Windows 2000/NT:   The default security provider is NTLM.
                /// </summary>
                LOGON32_PROVIDER_DEFAULT = 0,
                LOGON32_PROVIDER_WINNT35 = 1,
                LOGON32_PROVIDER_WINNT40 = 2,
                LOGON32_PROVIDER_WINNT50 = 3
            }
            public enum SecurityImpersonationLevel : int
            {
                /// <summary>
                /// The server process cannot obtain identification information about the client, 
                /// and it cannot impersonate the client. It is defined with no value given, and thus, 
                /// by ANSI C rules, defaults to a value of zero. 
                /// </summary>
                SecurityAnonymous = 0,

                /// <summary>
                /// The server process can obtain information about the client, such as security identifiers and privileges, 
                /// but it cannot impersonate the client. This is useful for servers that export their own objects, 
                /// for example, database products that export tables and views. 
                /// Using the retrieved client-security information, the server can make access-validation decisions without 
                /// being able to use other services that are using the client's security context. 
                /// </summary>
                SecurityIdentification = 1,

                /// <summary>
                /// The server process can impersonate the client's security context on its local system. 
                /// The server cannot impersonate the client on remote systems. 
                /// </summary>
                SecurityImpersonation = 2,

                /// <summary>
                /// The server process can impersonate the client's security context on remote systems. 
                /// NOTE: Windows NT:  This impersonation level is not supported.
                /// </summary>
                SecurityDelegation = 3,
            }

            /// <summary>
            /// https://github.com/Alachisoft/NosDB/blob/master/Src/Common/Security/SafeTokenHandle.cs
            /// https://github.com/mvelazc0/PurpleSharp/blob/master/PurpleSharp/Lib/Impersonator.cs
            /// </summary>
            public sealed class SafeTokenHandle : SafeHandleZeroOrMinusOneIsInvalid
            {
                private SafeTokenHandle() : base(true) { }

                [DllImport("kernel32.dll")]
                [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
                [SuppressUnmanagedCodeSecurity]
                [return: MarshalAs(UnmanagedType.Bool)]
                private static extern bool CloseHandle(IntPtr handle);

                protected override bool ReleaseHandle()
                {
                    if (handle == IntPtr.Zero)
                    {
                        return true;
                    }
                    var ret = CloseHandle(handle);
                    handle = IntPtr.Zero;
                    return ret;
                }
            }

            [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern bool LogonUser(string lpszUsername, string lpszDomain, string lpszPassword, LogonTypes dwLogonType, LogonProvider dwLogonProvider, out SafeTokenHandle phToken);

            [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern bool LogonUser(string lpszUsername, string lpszDomain, IntPtr phPassword, LogonTypes dwLogonType, LogonProvider dwLogonProvider, out SafeTokenHandle phToken);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern bool DuplicateToken(SafeTokenHandle ExistingTokenHandle, SecurityImpersonationLevel SECURITY_IMPERSONATION_LEVEL, out SafeTokenHandle DuplicateTokenHandle);

            #endregion

            #region Utility Methods
            public static SecureString PtrToSecureString(IntPtr p)
            {
                SecureString s = new SecureString();
                int i = 0;
                while (true)
                {
                    char c = (char)Marshal.ReadInt16(p, i++ * sizeof(short));
                    if (c == '\u0000')
                        break;
                    s.AppendChar(c);
                }
                s.MakeReadOnly();
                return s;
            }
            public static SecureString PtrToSecureString(IntPtr p, int length)
            {
                SecureString s = new SecureString();
                for (var i = 0; i < length; i++)
                    s.AppendChar((char)Marshal.ReadInt16(p, i * sizeof(short)));
                s.MakeReadOnly();
                return s;
            }
            #endregion
        }

    }

}
