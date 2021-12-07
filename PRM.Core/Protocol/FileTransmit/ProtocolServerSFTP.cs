using System;
using System.Text;
using Newtonsoft.Json;
using PRM.Core.Model;
using PRM.Core.Protocol.FileTransmit.Transmitters;
using Shawn.Utils;

namespace PRM.Core.Protocol.FileTransmit.SFTP
{
    public class ProtocolServerSFTP : ProtocolServerWithAddrPortUserPwdBase, IProtocolFileTransmittable
    {
        public static string ProtocolName = "SFTP";
        public ProtocolServerSFTP() : base(ProtocolName, $"{ProtocolName}.V1", ProtocolName)
        {
            base.UserName = "root";
            base.Port = "22";
        }

        private string _privateKey = "";

        [OtherName(Name = "PRM_SSH_PRIVATE_KEY_PATH")]
        public string PrivateKey
        {
            get => _privateKey;
            set => SetAndNotifyIfChanged(nameof(PrivateKey), ref _privateKey, value);
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
                var ret = JsonConvert.DeserializeObject<ProtocolServerSFTP>(jsonString);
                return ret;
            }
            catch (Exception e)
            {
                SimpleLogHelper.Debug(e);
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
            var sshKey = this.PrivateKey;
            if (sshKey == "")
                return new TransmitterSFtp(hostname, port, username, password);
            else
                return new TransmitterSFtp(hostname, port, username, Encoding.ASCII.GetBytes(sshKey));
        }

        public string GetStartupPath()
        {
            return StartupPath;
        }
    }
}