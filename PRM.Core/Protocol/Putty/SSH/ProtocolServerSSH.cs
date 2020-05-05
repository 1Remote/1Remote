using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PRM.Core.Protocol.RDP;

namespace PRM.Core.Protocol.Putty.SSH
{
    public class ProtocolServerSSH : ProtocolPutttyBase
    {
        public enum ESshVersion
        {
            V1 = 1,
            V2 = 2,
        }
        public ProtocolServerSSH() : base("SSH", "Putty.SSH.V1", "SSH")
        {
        }

        #region Conn

        private string _address;

        public string Address
        {
            get => _address;
            set
            {
                SetAndNotifyIfChanged(nameof(Address), ref _address, value);
            }
        }


        private int _port = 22;
        public int Port
        {
            get => _port > 0 ? _port : 22;
            set => SetAndNotifyIfChanged(nameof(Port), ref _port, value);
        }


        private string _userName = "";
        public string UserName
        {
            get => _userName;
            set => SetAndNotifyIfChanged(nameof(UserName), ref _userName, value);
        }

        private string _password;
        public string Password
        {
            get => _password;
            set
            {
                // TODO 当输入为明文时，执行加密
                SetAndNotifyIfChanged(nameof(Password), ref _password, value);
            }
        }

        private ESshVersion _sshVersion;
        public ESshVersion SshVersion
        {
            get => _sshVersion;
            set => SetAndNotifyIfChanged(nameof(SshVersion), ref _sshVersion, value);
        }

        #endregion

        public override string ToJsonString()
        {
            return JsonConvert.SerializeObject(this);
        }

        public override ProtocolServerBase CreateFromJsonString(string jsonString)
        {
            try
            {
                return JsonConvert.DeserializeObject<ProtocolServerRDP>(jsonString);
            }
            catch (Exception e)
            {
                return null;
            }
        }

        protected override string GetSubTitle()
        {
            return $"@{Address} ({UserName})";
        }

        public override string GetPuttyConnString()
        {
            var arg = $@" -load ""{base.SessionName}"" {Address} -P {Port} -l {UserName} -pw {Password} -{(int)SshVersion}";
            return arg;
        }
    }
}
