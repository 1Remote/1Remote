using System;
using System.Text;
using Newtonsoft.Json;
using PRM.Model.Protocol.Base;
using PRM.Model.Protocol.FileTransmit;
using PRM.Model.Protocol.FileTransmit.Transmitters;
using PRM.Service;
using Shawn.Utils;

namespace PRM.Model.Protocol
{
    // ReSharper disable once InconsistentNaming
    public class SFTP : ProtocolBaseWithAddressPortUserPwd, IFileTransmittable
    {
        public static string ProtocolName = "SFTP";
        public SFTP() : base(ProtocolName, $"{ProtocolName}.V1", ProtocolName)
        {
            base.UserName = "root";
            base.Port = "22";
        }

        private string _privateKey = "";

        [OtherName(Name = "PRM_SSH_PRIVATE_KEY_PATH")]
        public string PrivateKey
        {
            get => _privateKey;
            set => SetAndNotifyIfChanged(ref _privateKey, value);
        }

        private string _startupPath = "/";
        [OtherName(Name = "PRM_STARTUP_PATH")]
        public string StartupPath
        {
            get => _startupPath;
            set => SetAndNotifyIfChanged(ref _startupPath, value);
        }

        public override bool IsOnlyOneInstance()
        {
            return false;
        }

        public override ProtocolBase CreateFromJsonString(string jsonString)
        {
            try
            {
                var ret = JsonConvert.DeserializeObject<SFTP>(jsonString);
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

        public ITransmitter GeTransmitter()
        {
            var hostname = this.Address;
            int port = this.GetPort();
            var username = this.UserName;
            var password = IoC.Get<DataService>().DecryptOrReturnOriginalString(this.Password);
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