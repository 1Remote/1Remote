using System;
using Newtonsoft.Json;
using PRM.Core.Model;
using PRM.Core.Protocol.RDP;
using Shawn.Utils;
using Shawn.Utils.RdpFile;

namespace PRM.Core.Protocol.RDP
{
    public sealed class ProtocolServerRemoteApp : ProtocolServerWithAddrPortUserPwdBase
    {
        public static string ProtocolName = "RemoteApp";
        public ProtocolServerRemoteApp() : base(ProtocolName, "RemoteApp.V1", "RemoteApp", "APP")
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
        public RdpConfig ToRdpConfig(PrmContext context)
        {
            var rdpConfig = new RdpConfig($"{this.Address}:{this.GetPort()}", this.UserName, context.DataService.DecryptOrReturnOriginalString(Password));
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
            rdpConfig.AudioMode = 1;
            rdpConfig.AudioCaptureMode = 1;
            rdpConfig.AutoReconnectionEnabled = 1;

            rdpConfig.RemoteApplicationMode = 1;
            rdpConfig.RemoteApplicationName = RemoteApplicationName;
            rdpConfig.RemoteApplicationProgram = RemoteApplicationProgram;

            rdpConfig.UseMultimon = 1;

            return rdpConfig;
        }
    }
}