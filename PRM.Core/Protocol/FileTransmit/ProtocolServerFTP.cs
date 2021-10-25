using System;
using System.Text;
using Newtonsoft.Json;
using PRM.Core.Model;
using PRM.Core.Protocol.FileTransmit.Transmitters;
using Shawn.Utils;

namespace PRM.Core.Protocol.FileTransmit.FTP
{
    public class ProtocolServerFTP : ProtocolServerWithAddrPortUserPwdBase, IProtocolFileTransmittable
    {
        public static string ProtocolName = "FTP";
        public ProtocolServerFTP() : base("FTP", "FTP.V1", "FTP")
        {
        }

        private string _startupPath = "/";
        [OtherName(Name = "PRM_STARTUP_PATH")]
        public string StartupPath
        {
            get => _startupPath;
            set => SetAndNotifyIfChanged(nameof(StartupPath), ref _startupPath, value);
        }

        public override bool IsOnlyOneInstance()
        {
            return false;
        }

        public override ProtocolServerBase CreateFromJsonString(string jsonString)
        {
            try
            {
                var ret = JsonConvert.DeserializeObject<ProtocolServerFTP>(jsonString);
                return ret;
            }
            catch
            {
                return null;
            }
        }

        public override double GetListOrder()
        {
            return 4;
        }

        public ITransmitter GeTransmitter(PrmContext context)
        {
            var hostname = this.Address;
            int port = this.GetPort();
            var username = this.UserName;
            var password = context.DataService.DecryptOrReturnOriginalString(this.Password);
            return new TransmitterFtp(hostname, port, username, password);
        }

        public string GetStartupPath()
        {
            return StartupPath;
        }
    }
}
