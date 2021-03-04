using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PRM.Core.Model;
using PRM.Core.Protocol.RDP;
using Shawn.Utils;

namespace PRM.Core.Protocol.Putty.SSH
{
    public class ProtocolServerSSH : ProtocolServerWithAddrPortUserPwdBase, IPuttyConnectable
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
            set => SetAndNotifyIfChanged(nameof(SshVersion), ref _sshVersion, value);
        }


        private string _startupAutoCommand = "";
        public string StartupAutoCommand
        {
            get => _startupAutoCommand;
            set => SetAndNotifyIfChanged(nameof(StartupAutoCommand), ref _startupAutoCommand, value);
        }

        private string _externalKittySessionConfigPath;
        public string ExternalKittySessionConfigPath
        {
            get => _externalKittySessionConfigPath;
            set => SetAndNotifyIfChanged(nameof(ExternalKittySessionConfigPath), ref _externalKittySessionConfigPath, value);
        }

        public override bool IsOnlyOneInstance()
        {
            return false;
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
                SimpleLogHelper.Debug(e);
                return null;
            }
        }

        public override double GetListOrder()
        {
            return 1;
        }


        public string GetPuttyConnString(PrmContext context)
        {
            //var arg = $"-ssh {Address} -P {Port} -l {UserName} -pw {Password} -{(int)SshVersion}";
            var arg = $@" -load ""{this.GetSessionName()}"" {Address} -P {Port} -l {UserName} -pw {context.DbOperator.DecryptOrReturnOriginalString(Password)} -{(int)SshVersion}";
            if (!string.IsNullOrWhiteSpace(StartupAutoCommand))
                arg = arg + $" -cmd \"{StartupAutoCommand.Replace(@"""", @"\""")}\"";
            return arg;
        }

        [JsonIgnore]
        public ProtocolServerBase ProtocolServerBase => this;
    }
}
