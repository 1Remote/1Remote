using System;
using Newtonsoft.Json;
using PRM.Core.Model;
using PRM.Core.Protocol.RDP;
using RdpHelper;
using Shawn.Utils;

namespace PRM.Core.Protocol.RDP
{
    public sealed class ProtocolServerRemoteApp : ProtocolServerWithAddrPortUserPwdBase
    {
        public class LocalSetting : NotifyPropertyChangedBase
        {
            private bool _fullScreenLastSessionIsFullScreen = false;
            public bool FullScreenLastSessionIsFullScreen
            {
                get => _fullScreenLastSessionIsFullScreen;
                set => SetAndNotifyIfChanged(nameof(FullScreenLastSessionIsFullScreen), ref _fullScreenLastSessionIsFullScreen, value);
            }

            private int _fullScreenLastSessionScreenIndex = -1;
            public int FullScreenLastSessionScreenIndex
            {
                get => _fullScreenLastSessionScreenIndex;
                set => SetAndNotifyIfChanged(nameof(FullScreenLastSessionScreenIndex), ref _fullScreenLastSessionScreenIndex, value);
            }
        }

        public ProtocolServerRemoteApp() : base("RemoteApp", "RemoteApp.V1", "RemoteApp", "APP")
        {
            base.Port = "3389";
            base.UserName = "Administrator";
        }


        private string _remoteApplicationName = "";
        public string RemoteApplicationName
        {
            get => _remoteApplicationName;
            set => SetAndNotifyIfChanged(nameof(RemoteApplicationName), ref _remoteApplicationName, value);
        }

        private string _remoteApplicationProgram = "";
        public string RemoteApplicationProgram
        {
            get => _remoteApplicationProgram;
            set => SetAndNotifyIfChanged(nameof(RemoteApplicationProgram), ref _remoteApplicationProgram, value);
        }


        public override bool IsOnlyOneInstance()
        {
            return false;
        }

        public override ProtocolServerBase CreateFromJsonString(string jsonString)
        {
            try
            {
                return JsonConvert.DeserializeObject<ProtocolServerRemoteApp>(jsonString);
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
            var rdpConfig = new RdpConfig($"{this.Address}:{this.Port}", this.UserName, this.GetDecryptedPassWord());
            rdpConfig.AuthenticationLevel = 0;
            rdpConfig.KeyboardHook = 0;
            rdpConfig.AudioMode = 2;
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
            rdpConfig.AudioMode = 0;
            rdpConfig.AudioCaptureMode = 1;
            rdpConfig.AutoReconnectionEnabled = 1;

            rdpConfig.RemoteApplicationMode = 1;
            rdpConfig.RemoteApplicationName = RemoteApplicationName;
            rdpConfig.RemoteApplicationProgram = RemoteApplicationProgram;

            rdpConfig.SpanMonitors = 1;
            rdpConfig.UseMultimon = 1;

            return rdpConfig;
        }
    }
}
