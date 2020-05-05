using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PRM.Core.Protocol.RDP;

namespace PRM.Core.Protocol.Putty.SSH
{
    public class ProtocolServerSSH : ProtocolServerWithAddrBase, IPuttyConnectable
    {
        public enum ESshVersion
        {
            V1 = 1,
            V2 = 2,
        }
        public ProtocolServerSSH() : base("SSH", "Putty.SSH.V1", "SSH")
        {
        }
        

        private string _privateKey = "";
        public string PrivateKey
        {
            get => _privateKey;
            set => SetAndNotifyIfChanged(nameof(PrivateKey), ref _privateKey, value);
        }

        private ESshVersion _sshVersion = ESshVersion.V2;

        public ESshVersion SshVersion
        {
            get => _sshVersion;
            set => 
                SetAndNotifyIfChanged(nameof(SshVersion), ref _sshVersion, value);
        }


        public override string ToJsonString()
        {
            return JsonConvert.SerializeObject(this);
        }

        public override ProtocolServerBase CreateFromJsonString(string jsonString)
        {
            try
            {
                return JsonConvert.DeserializeObject<ProtocolServerSSH>(jsonString);
            }
            catch (Exception e)
            {
                return null;
            }
        }

        protected override string GetSubTitle()
        {
            return $"@{SshVersion} ({UserName})";
        }

        public string GetPuttyConnString()
        {
            // var arg = $@" -load ""{PuttyOption.SessionName}"" {IP} -P {PORT} -l {user} -pw {pdw} -{ssh version}";
            var arg = $@" -load ""{this.GetSessionName()}"" {Address} -P {Port} -l {UserName} -pw {Password} -{(int)SshVersion}";
            return arg;
        }

        [JsonIgnore]
        public ProtocolServerBase ProtocolServerBase => this;
    }
}
