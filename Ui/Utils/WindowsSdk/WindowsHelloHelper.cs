//#define KeyCredentialManager
using System;
using System.Threading.Tasks;
using _1RM.Utils.WindowsApi;
using Windows.Security.Credentials;
using Windows.Security.Credentials.UI;

namespace _1RM.Utils.WindowsSdk
{
    public class WindowsHelloHelper
    {
        public static bool IsOsSupported => WindowsVersionHelper.IsLowerThanWindows10() == false;

        public static async Task<bool> HelloIsAvailable()
        {
            if (IsOsSupported == false)
            {
                return true;
            }

#if KeyCredentialManager
            var isAvailable = await KeyCredentialManager.IsSupportedAsync();
#else
            var isAvailable = await UserConsentVerifier.CheckAvailabilityAsync() == UserConsentVerifierAvailability.Available;
#endif
            return isAvailable;
        }

        public static async Task<bool?> HelloVerifyAsync(string message)
        {
            if (IsOsSupported == false)
            {
                return true;
            }

            var isAvailable = await KeyCredentialManager.IsSupportedAsync();
            if (isAvailable)
            {
#if KeyCredentialManager
                var result = await KeyCredentialManager.RequestCreateAsync("UserConsentVerifier", KeyCredentialCreationOption.ReplaceExisting);
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
#else
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
#endif
            }
            return false;
        }

    }
}
