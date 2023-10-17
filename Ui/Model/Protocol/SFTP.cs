using System;
using Newtonsoft.Json;
using _1RM.Model.Protocol.Base;
using _1RM.Model.Protocol.FileTransmit;
using _1RM.Model.Protocol.FileTransmit.Transmitters;
using _1RM.Utils;
using Shawn.Utils;

namespace _1RM.Model.Protocol
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

        private string _startupPath = "/";
        [OtherName(Name = "STARTUP_PATH")]
        public string StartupPath
        {
            get => _startupPath;
            set => SetAndNotifyIfChanged(ref _startupPath, value);
        }

        public override bool IsOnlyOneInstance()
        {
            return false;
        }

        public override ProtocolBase? CreateFromJsonString(string jsonString)
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
            var password = UnSafeStringEncipher.DecryptOrReturnOriginalString(this.Password) ?? this.Password;
            var sshKeyPath = this.PrivateKey;
            if (sshKeyPath == "")
                return new TransmitterSFtp(hostname, port, username, password, true);
            else
                return new TransmitterSFtp(hostname, port, username, sshKeyPath, false);
        }

        public string GetStartupPath()
        {
            return StartupPath;
        }



        public override Credential GetCredential()
        {
            var c = new Credential()
            {
                Address = Address,
                Port = Port,
                Password = Password,
                UserName = UserName,
                PrivateKeyPath = PrivateKey,
            };
            return c;
        }
        public override void SetCredential(in Credential credential)
        {
            base.SetCredential(credential);
            PrivateKey = credential.PrivateKeyPath;
        }

        public override bool ShowUserNameInput()
        {
            return true;
        }

        public override bool ShowPasswordInput()
        {
            return true;
        }

        public override bool ShowPrivateKeyInput()
        {
            return true;
        }
    }
}