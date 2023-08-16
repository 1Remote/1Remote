using System;
using System.Globalization;
using System.Threading.Tasks;
using Windows.Security.Credentials;
using Shawn.Utils;
using _1RM.Service;
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

        //public static async Task<bool?> HelloVerifyAsyncIfIsSupport(bool defaultReturn = true)
        //{
        //    if (WindowsHelloHelper.IsOsSupported)
        //    {
        //        if (await WindowsHelloHelper.HelloIsAvailable() == true)
        //        {
        //            return await HelloVerifyAsync();
        //        }
        //    }
        //    return defaultReturn;
        //}

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

    }
}
