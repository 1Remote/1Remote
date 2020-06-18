using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PRM.Core.Model;
using PRM.Core.Protocol.RDP;

namespace PRM.Core.Protocol.Putty.SSH
{
    public class ProtocolServerSSH : ProtocolServerWithAddrPortUserPwdBase, IPuttyConnectable
    {
        public enum ESshVersion
        {
            V1 = 1,
            V2 = 2,
        }
        public ProtocolServerSSH() : base("SSH", "Putty.SSH.V1", "SSH", false)
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
            set => SetAndNotifyIfChanged(nameof(SshVersion), ref _sshVersion, value);
        }

        public override ProtocolServerBase CreateFromJsonString(string jsonString)
        {
            try
            {
                var ret = JsonConvert.DeserializeObject<ProtocolServerSSH>(jsonString);
                return ret;
            }
            catch (Exception e)
            {
                return null;
            }
        }


        public string GetPuttyConnString()
        {
            //var arg = $"-ssh {Address} -P {Port} -l {UserName} -pw {Password} -{(int)SshVersion}";
            if(SystemConfig.Instance.DataSecurity.Rsa != null)
                return $@" -load ""{this.GetSessionName()}"" {Address} -P {Port} -l {UserName} -pw {SystemConfig.Instance.DataSecurity.Rsa.DecodeOrNull(Password) ?? ""} -{(int)SshVersion}";
            return $@" -load ""{this.GetSessionName()}"" {Address} -P {Port} -l {UserName} -pw {Password} -{(int)SshVersion}";
        }

        [JsonIgnore]
        public ProtocolServerBase ProtocolServerBase => this;
    }
}
