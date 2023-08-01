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

namespace _1RM.Utils.Windows
{
    public class WindowsHelloHelper
    {
        private static bool _isOsSupported = false;
        private static bool _isAvailable = false;
        private static string _accountId = DateTime.Now.ToString(CultureInfo.InvariantCulture);


        /// <summary>
        /// Checks to see if Passport is ready to be used.
        /// 
        /// Passport has dependencies on:
        ///     1. Having a connected Microsoft Account
        ///     2. Having a Windows PIN set up for that _account on the local machine
        /// </summary>
        public static async Task<bool> Init(string appName)
        {
            _isOsSupported = !WindowsVersionHelper.IsLowerThanWindows10();
            if (_isOsSupported)
            {
                _isAvailable = await KeyCredentialManager.IsSupportedAsync();
                //_isAvailable = await UserConsentVerifier.CheckAvailabilityAsync();
                if (_isAvailable == false)
                {
                    // Key credential is not enabled yet as user 
                    // needs to connect to a Microsoft Account and select a PIN in the connecting flow.
                    SimpleLogHelper.Warning("Microsoft Passport is not setup! Please go to Windows Settings and set up a PIN to use it.");
                }
                _accountId = appName;
            }
            else
            {
                SimpleLogHelper.Warning("Microsoft Passport is not supported on current os!");
            }
            return _isAvailable;
        }

        public static bool IsOsSupported => _isOsSupported;

        public static async Task<bool> HelloIsAvailable()
        {
            if (_isOsSupported)
            {
                _isAvailable = await KeyCredentialManager.IsSupportedAsync();
                return _isAvailable;
            }
            else
                return true;
        }

        public static async Task<bool?> HelloVerifyAsync(string message)
        {
            if (!_isOsSupported)
            {
                return true;
            }
            _isAvailable = await KeyCredentialManager.IsSupportedAsync();


            if (_isAvailable)
            {
                var consentResult = await UserConsentVerifier.RequestVerificationAsync(message);
                switch (consentResult)
                {
                    case UserConsentVerificationResult.Verified:
                        return true;
                    case UserConsentVerificationResult.Canceled:
                        return null;
                    case UserConsentVerificationResult.DeviceNotPresent:
                    case UserConsentVerificationResult.NotConfiguredForUser:
                    case UserConsentVerificationResult.DisabledByPolicy:
                    case UserConsentVerificationResult.DeviceBusy:
                    case UserConsentVerificationResult.RetriesExhausted:
                    default:
                        break;
                }
                return false;


                //var result = await KeyCredentialManager.RequestCreateAsync(_accountId + "-login", KeyCredentialCreationOption.ReplaceExisting);
                //switch (result.Status)
                //{
                //    case KeyCredentialStatus.Success:
                //        return true;
                //    case KeyCredentialStatus.UserCanceled:
                //        return null;
                //    case KeyCredentialStatus.UnknownError:
                //    case KeyCredentialStatus.NotFound:
                //    case KeyCredentialStatus.UserPrefersPassword:
                //    case KeyCredentialStatus.CredentialAlreadyExists:
                //    case KeyCredentialStatus.SecurityDeviceLocked:
                //    default:
                //        return false;
                //}
            }
            return false;
        }
    }
}
