using System;
using Newtonsoft.Json;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.Utils;
using _1RM.Utils.RdpFile;
using Shawn.Utils;

namespace _1RM.Model.Protocol
{
    public sealed class RdpApp : ProtocolBaseWithAddressPortUserPwd
    {
        public static string ProtocolName = "RemoteApp";
        public RdpApp() : base(ProtocolName, "RemoteApp.V1", "RemoteApp")
        {
            base.Port = "3389";
            base.UserName = "Administrator";
        }

        private string _remoteApplicationName = "";
        public string RemoteApplicationName
        {
            get => _remoteApplicationName;
            set => SetAndNotifyIfChanged(ref _remoteApplicationName, value);
        }

        private string _remoteApplicationProgram = "";

        public string RemoteApplicationProgram
        {
            get => _remoteApplicationProgram;
            set => SetAndNotifyIfChanged(ref _remoteApplicationProgram, value);
        }

        private EAudioRedirectionMode? _audioRedirectionMode = EAudioRedirectionMode.RedirectToLocal;
        public EAudioRedirectionMode? AudioRedirectionMode
        {
            get => _audioRedirectionMode;
            set => SetAndNotifyIfChanged(ref _audioRedirectionMode, value);
        }

        private EAudioQualityMode? _audioQualityMode = EAudioQualityMode.Dynamic;
        public EAudioQualityMode? AudioQualityMode
        {
            get => _audioQualityMode;
            set => SetAndNotifyIfChanged(ref _audioQualityMode, value);
        }


        private string _rdpFileAdditionalSettings = "";
        public string RdpFileAdditionalSettings
        {
            get => _rdpFileAdditionalSettings;
            set => SetAndNotifyIfChanged(ref _rdpFileAdditionalSettings, value);
        }

        public override bool IsOnlyOneInstance()
        {
            return false;
        }

        public override ProtocolBase? CreateFromJsonString(string jsonString)
        {
            try
            {
                return JsonConvert.DeserializeObject<RdpApp>(jsonString);
            }
            catch (Exception e)
            {
                SimpleLogHelper.Debug(e);
                return null;
            }
        }

        public override double GetListOrder()
        {
            return 0.1;
        }

        /// <summary>
        /// To rdp file object
        /// </summary>
        /// <returns></returns>
        public RdpConfig ToRdpConfig()
        {
            var rdpConfig = new RdpConfig(DisplayName, $"{this.Address}:{this.GetPort()}", 
                this.UserName, UnSafeStringEncipher.DecryptOrReturnOriginalString(Password),
                RdpFileAdditionalSettings);

            rdpConfig.AuthenticationLevel = 0;
            rdpConfig.KeyboardHook = 0;
            //rdpConfig.AudioMode = 2;
            rdpConfig.AudioCaptureMode = 0;

            rdpConfig.RedirectPosDevices = 0;
            rdpConfig.DeviceStoreDirect = "";
            rdpConfig.DriveStoreDirect = "";
            rdpConfig.RedirectDrives = 0;
            rdpConfig.RedirectComPorts = 1;
            rdpConfig.RedirectClipboard = 1;
            rdpConfig.RedirectPrinters = 0;
            rdpConfig.RedirectSmartCards = 1;
            rdpConfig.KeyboardHook = 2;
            rdpConfig.AudioCaptureMode = 1;
            rdpConfig.AutoReconnectionEnabled = 1;

            rdpConfig.RemoteApplicationMode = 1;
            rdpConfig.RemoteApplicationName = RemoteApplicationName;
            rdpConfig.RemoteApplicationProgram = RemoteApplicationProgram;

            rdpConfig.UseMultimon = 1;


            if (this.AudioRedirectionMode == EAudioRedirectionMode.RedirectToLocal)
                rdpConfig.AudioMode = 0;
            else if (this.AudioRedirectionMode == EAudioRedirectionMode.LeaveOnRemote)
                rdpConfig.AudioMode = 1;
            else if (this.AudioRedirectionMode == EAudioRedirectionMode.Disabled)
                rdpConfig.AudioMode = 2;

            if (this.AudioQualityMode == EAudioQualityMode.Dynamic)
                rdpConfig.AudioQualityMode = 0;
            else if (this.AudioQualityMode == EAudioQualityMode.Medium)
                rdpConfig.AudioQualityMode = 1;
            else if (this.AudioQualityMode == EAudioQualityMode.High)
                rdpConfig.AudioQualityMode = 2;

            return rdpConfig;
        }
        #region IDataErrorInfo

        [JsonIgnore]
        public override string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(RemoteApplicationName):
                        {
                            if (string.IsNullOrWhiteSpace(RemoteApplicationName))
                                return IoC.Translate(LanguageService.CAN_NOT_BE_EMPTY);
                            break;
                        }
                    case nameof(RemoteApplicationProgram):
                        {
                            if (string.IsNullOrWhiteSpace(RemoteApplicationProgram))
                                return IoC.Translate(LanguageService.CAN_NOT_BE_EMPTY);
                            break;
                        }
                    default:
                        {
                            return base[columnName];
                        }
                }
                return "";
            }
        }
        #endregion
        public override string GetHelpUrl()
        {
            return "https://1remote.org/usage/protocol/especial/remoteapp/";
        }
    }
}