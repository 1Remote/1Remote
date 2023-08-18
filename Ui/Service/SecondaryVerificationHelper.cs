using System;
using System.Threading.Tasks;
using _1RM.View.Utils;
using _1RM.Utils.WindowsSdk;
using _1RM.Utils.WindowsApi.Credential;

namespace _1RM.Service
{
    public static class SecondaryVerificationHelper
    {
        private static bool? _isEnabled = null;
        private static DateTime _lastVerifyTime = DateTime.MinValue;

        public static void SetEnabled(bool enable)
        {
            if (_isEnabled != enable)
            {
                // TODO save to security config
            }
            _isEnabled = enable;
        }

        public static bool GetEnabled()
        {
            if (_isEnabled != null)
            {
                return (bool)_isEnabled;
            }

            // TODO load from security config
            return true;
        }

        public static async Task<bool?> VerifyAsyncUi(bool defaultReturn = false, bool returnUntilOkOrCancel = true)
        {
            if (GetEnabled() == false)
                return true;

            bool? result;
            bool widowsHelloIsOk = WindowsHelloHelper.IsOsSupported && await WindowsHelloHelper.HelloIsAvailable() == true;
            int counter = 0;
            MaskLayerController.ShowProcessingRing();
            while (true)
            {

                if (!widowsHelloIsOk)
                {
                    try
                    {
                        string title = "TXT:验证你的账户";
                        string message = "TXT:请输入当前Windows的凭据";
                        if (counter > 0)
                        {
                            title = "TXT: 验证错误，请输入正确的凭据";
                        }
                        var ret = CredentialPrompt.LogonUserWithWindowsCredential(title, message,
                            null, // new WindowInteropHelper(this).Handle
                            null, null,
                            0
                            | (uint)CredentialPrompt.PromptForWindowsCredentialsFlag.CREDUIWIN_GENERIC
                            | (uint)CredentialPrompt.PromptForWindowsCredentialsFlag.CREDUIWIN_ENUMERATE_CURRENT_USER
                        );
                        if (ret == CredentialPrompt.LogonUserStatus.Success)
                        {
                            result = true;
                        }
                        else if (ret == CredentialPrompt.LogonUserStatus.Cancel)
                        {
                            result = null;
                        }
                        else
                        {
                            result = defaultReturn;
                        }
                    }
                    catch (Exception)
                    {
                        result = defaultReturn;
                    }
                    //if (defaultReturn == false)
                    //    MessageBoxHelper.Warning(IoC.Get<LanguageService>().Translate("Windows Hello is currently unavailable, sensitive operations will be denied! Please set up a PIN or enable Windows Hello."));
                    //return defaultReturn;
                }
                else
                {
                    result = await WindowsHelloHelper.HelloVerifyAsync();
                }

                if (result != false)
                    break;
                if (returnUntilOkOrCancel == false)
                    break;
                counter++;
            }

            MaskLayerController.HideMask();
            return result;
        }


        public static void VerifyAsyncUiCallBack(Action<bool?> callBack, bool defaultReturn = false, bool returnUntilOkOrCancel = true)
        {
            Task.Factory.StartNew(async () =>
            {
                bool? result = await VerifyAsyncUi(defaultReturn, returnUntilOkOrCancel);
                callBack.Invoke(result);
            });
        }
    }
}
