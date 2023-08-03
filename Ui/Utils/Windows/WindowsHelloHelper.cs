using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using Windows.Security.Credentials;
using Shawn.Utils;
using static Microsoft.WindowsAPICodePack.Shell.PropertySystem.SystemProperties.System;
using Windows.Security.Credentials.UI;
using _1RM.View.Utils;

namespace _1RM.Utils.Windows
{
    public class WindowsHelloHelper
    {
        private static string _accountId = DateTime.Now.ToString(CultureInfo.InvariantCulture);


        /// <summary>
        /// Checks to see if Passport is ready to be used.
        /// 
        /// Passport has dependencies on:
        ///     1. Having a connected Microsoft Account
        ///     2. Having a Windows PIN set up for that _account on the local machine
        /// </summary>
        public static async void Init(string appName)
        {
            _accountId = appName;
            if (IsOsSupported)
            {
                var isAvailable = await KeyCredentialManager.IsSupportedAsync();
                if (isAvailable == false)
                {
                    // Key credential is not enabled yet as user 
                    // needs to connect to a Microsoft Account and select a PIN in the connecting flow.
                    SimpleLogHelper.Warning("Microsoft Passport is not setup! Please go to Windows Settings and set up a PIN to use it.");
                }
            }
            else
            {
                SimpleLogHelper.Warning("Microsoft Passport is not supported on current os!");
            }
        }

        public static bool IsOsSupported => WindowsVersionHelper.IsLowerThanWindows10() == false;

        public static async Task<bool> HelloIsAvailable()
        {
            if (IsOsSupported == false)
            {
                return true;
            }

            var isAvailable = await KeyCredentialManager.IsSupportedAsync();
            return isAvailable;
        }

        public static async Task<bool?> HelloVerifyAsync()
        {
            if (IsOsSupported == false)
            {
                return true;
            }

            var isAvailable = await KeyCredentialManager.IsSupportedAsync();
            if (isAvailable)
            {
                //var consentResult = await UserConsentVerifier.RequestVerificationAsync(message);
                //switch (consentResult)
                //{
                //    case UserConsentVerificationResult.Verified:
                //        return true;
                //    case UserConsentVerificationResult.Canceled:
                //        return null;
                //    case UserConsentVerificationResult.DeviceNotPresent:
                //    case UserConsentVerificationResult.NotConfiguredForUser:
                //    case UserConsentVerificationResult.DisabledByPolicy:
                //    case UserConsentVerificationResult.DeviceBusy:
                //    case UserConsentVerificationResult.RetriesExhausted:
                //    default:
                //        break;
                //}
                //return false;


                var result = await KeyCredentialManager.RequestCreateAsync(_accountId + "-UserConsentVerifier", KeyCredentialCreationOption.ReplaceExisting);
                switch (result.Status)
                {
                    case KeyCredentialStatus.Success:
                        return true;
                    case KeyCredentialStatus.UserCanceled:
                        return null;
                    case KeyCredentialStatus.UnknownError:
                    case KeyCredentialStatus.NotFound:
                    case KeyCredentialStatus.UserPrefersPassword:
                    case KeyCredentialStatus.CredentialAlreadyExists:
                    case KeyCredentialStatus.SecurityDeviceLocked:
                    default:
                        return false;
                }
            }
            return false;
        }

        public static async Task<bool?> StrictHelloVerifyAsyncUi()
        {
            if (WindowsHelloHelper.IsOsSupported)
            {
                if (await WindowsHelloHelper.HelloIsAvailable() != true)
                {
                    MessageBoxHelper.Warning("TXT: 当前 Windows Hello 不可用，敏感操作将被拒绝！请设置 PIN 或启用 Windows Hello。");
                    return false;
                    //SimpleLogHelper.Info("WindowsHelloIsAvailable == false");
                }
                else
                {
                    MaskLayerController.ShowProcessingRing("TXT: 请完成 Windows Hello 权限验证。");
                    if (await WindowsHelloHelper.HelloVerifyAsync() != true)
                    {
                        return true;
                    }
                    SimpleLogHelper.DebugInfo("Hello passed");
                }
            }
            else
            {
                MessageBoxHelper.Warning("TXT: 当前 Windows Hello 不可用，敏感操作将被拒绝！请设置 PIN 或启用 Windows Hello。");
                SimpleLogHelper.DebugInfo("IsOsSupported == false");
                return false;
            }
            return false;
        }
    }
}
