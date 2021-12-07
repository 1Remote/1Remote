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
using PRM.Core.Protocol.Runner.Default;
using Shawn.Utils;

namespace PRM.Core.Protocol.Putty.SSH
{
    public class ProtocolServerSSH : ProtocolServerWithAddrPortUserPwdBase, IKittyConnectable
    {
        public static string ProtocolName = "SSH";
        public ProtocolServerSSH() : base(ProtocolServerSSH.ProtocolName, $"Putty.{ProtocolServerSSH.ProtocolName}.V1", ProtocolServerSSH.ProtocolName)
        {
            base.UserName = "root";
            base.Port = "22";
        }

        [OtherNameAttribute(Name = "PRM_SESSION_NAME")]
        public string SessionName => this.GetSessionName();

        private string _privateKey = "";

        [OtherName(Name = "PRM_SSH_PRIVATE_KEY_PATH")]
        public string PrivateKey
        {
            get => _privateKey;
            set => SetAndNotifyIfChanged(nameof(PrivateKey), ref _privateKey, value);
        }

        private int? _sshVersion = 2;

        [OtherNameAttribute(Name = "PRM_SSH_VERSION")]
        public int? SshVersion
        {
            get => _sshVersion;
            set => SetAndNotifyIfChanged(nameof(SshVersion), ref _sshVersion, value);
        }

        private string _startupAutoCommand = "";

        [OtherNameAttribute(Name = "PRM_STARTUP_AUTO_COMMAND")]
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
            var ssh = this.Clone() as ProtocolServerSSH;
            ssh.ConnectPreprocess(context);

            // var arg = $"-ssh {port} {user} {pw} {server}";
            // var arg = $@" -load ""{PuttyOption.SessionName}"" {IP} -P {PORT} -l {user} -pw {pdw} -{ssh version}";
            //var arg = $"-ssh {Address} -P {Port} -l {UserName} -pw {Password} -{(int)SshVersion}";
            //var template = $@" -load ""%SessionName%"" %Address% -P %Port% -l %UserName% -pw %Password% -%SshVersion% -cmd ""%StartupAutoCommand%""";
            //var properties = this.GetType().GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            //foreach (var property in properties)
            //{
            //    if (property.CanRead && property.GetMethod.IsPublic)
            //    {
            //        var val = property.GetValue(serverBase);
            //        var key = property.Name;
            //        template = template.Replace($"%{key}%", val?.ToString() ?? "");
            //    }
            //}

            //var arg = $@" -load ""{serverBase.SessionName}"" {serverBase.Address} -P {serverBase.Port} -l {serverBase.UserName} -pw {serverBase.Password} -{(int)serverBase.SshVersion} -cmd ""{serverBase.StartupAutoCommand}""";

            var template = $@" -load ""%PRM_SESSION_NAME%"" %PRM_HOSTNAME% -P %PRM_PORT% -l %PRM_USERNAME% -pw %PRM_PASSWORD% -%PRM_SSH_VERSION% -cmd ""%PRM_STARTUP_AUTO_COMMAND%""";
            var arg = OtherNameAttributeExtensions.Replace(ssh, template);
            return " " + arg;
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