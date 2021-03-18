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
        public enum ESshVersion
        {
            V1 = 1,
            V2 = 2,
        }

        public ProtocolServerSFTP() : base("SFTP", "SFTP.V1", "SFTP")
        {
        }

        private string _privateKey = "";

        public string PrivateKey
        {
            get => _privateKey;
            set => SetAndNotifyIfChanged(nameof(PrivateKey), ref _privateKey, value);
        }

        private string _startupPath = "/";

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

        [JsonIgnore]
        public ProtocolServerBase ProtocolServerBase => this;

        public ITransmitter GeTransmitter(PrmContext context)
        {
            var hostname = this.Address;
            int port = this.GetPort();
            var username = this.UserName;
            var password = context.DbOperator.DecryptOrReturnOriginalString(this.Password);
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