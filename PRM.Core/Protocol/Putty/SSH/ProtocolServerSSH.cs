using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PRM.Core.External.KiTTY;
using PRM.Core.Model;
using PRM.Core.Protocol.RDP;
using Shawn.Utils;

namespace PRM.Core.Protocol.Putty.SSH
{
    public class ProtocolServerSSH : ProtocolServerWithAddrPortUserPwdBase, IKittyConnectable
    {
        public enum ESshVersion
        {
            V1 = 1,
            V2 = 2,
        }

        public static string ProtocolName = "SSH";
        public ProtocolServerSSH() : base(ProtocolServerSSH.ProtocolName, $"Putty.{ProtocolServerSSH.ProtocolName}.V1", ProtocolServerSSH.ProtocolName)
        {
        }

        public string SessionName => this.GetSessionName();

        private string _privateKey = "";

        public string PrivateKey
        {
            get => _privateKey;
            set => SetAndNotifyIfChanged(nameof(PrivateKey), ref _privateKey, value);
        }

        private ESshVersion? _sshVersion = ESshVersion.V2;

        public ESshVersion? SshVersion
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


        private bool _openSftpOnConnected = false;
        public bool OpenSftpOnConnected
        {
            get => _openSftpOnConnected;
            set => SetAndNotifyIfChanged(nameof(OpenSftpOnConnected), ref _openSftpOnConnected, value);
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
            // var arg = $"-ssh {port} {user} {pw} {server}";
            // var arg = $@" -load ""{PuttyOption.SessionName}"" {IP} -P {PORT} -l {user} -pw {pdw} -{ssh version}";
            //var arg = $"-ssh {Address} -P {Port} -l {UserName} -pw {Password} -{(int)SshVersion}";
            var template = $@" -load ""%SessionName%"" %Address% -P %Port% -l %UserName% -pw %Password% -%SshVersion% -cmd ""%StartupAutoCommand%""";
            var serverBase = this.Clone() as ProtocolServerSSH;
            serverBase.ConnectPreprocess(context);
            var properties = this.GetType().GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            foreach (var property in properties)
            {
                if (property.CanRead && property.GetMethod.IsPublic)
                {
                    var val = property.GetValue(serverBase);
                    var key = property.Name;
                    template = template.Replace($"%{key}%", val?.ToString() ?? "");
                }
            }

            var arg = $@" -load ""{SessionName}"" {Address} -P {Port} -l {UserName} -pw {context.DataService.DecryptOrReturnOriginalString(Password)} -{(int)SshVersion} -cmd ""{serverBase.StartupAutoCommand}""";
            return arg;
        }

        [JsonIgnore]
        public ProtocolServerBase ProtocolServerBase => this;

        public string GetExeFullPath()
        {
            return this.GetKittyExeFullName();
        }

        public string GetExeArguments(PrmContext context)
        {
            return GetPuttyConnString(context);
        }

        public override void ConnectPreprocess(PrmContext context)
        {
            base.ConnectPreprocess(context);
            StartupAutoCommand = StartupAutoCommand.Replace(@"""", @"\""");
        }
    }
}