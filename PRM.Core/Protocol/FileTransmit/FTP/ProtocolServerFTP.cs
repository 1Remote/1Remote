using System;
using System.Text;
using Newtonsoft.Json;
using PRM.Core.Protocol.FileTransmit.Transmitters;
using PRM.Core.Protocol.FileTransmitter;

namespace PRM.Core.Protocol.FileTransmit.FTP
{
    public class ProtocolServerFTP : ProtocolServerWithAddrPortUserPwdBase, IProtocolFileTransmittable
    {
        public enum ESshVersion
        {
            V1 = 1,
            V2 = 2,
        }
        public ProtocolServerFTP() : base("FTP", "FTP.V1", "FTP")
        {
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

        [JsonIgnore]
        public ProtocolServerBase ProtocolServerBase => this;

        public ITransmitter GeTransmitter()
        {
            var hostname = this.Address;
            int port = this.GetPort();
            var username = this.UserName;
            var password = this.GetDecryptedPassWord();
                return new TransmitterFtp(hostname, port, username, password);
        }

        public string GetStartupPath()
        {
            return StartupPath;
        }
    }
}
