using System;
using Newtonsoft.Json;
using PRM.Model.Protocol.Base;
using PRM.Utils.KiTTY;
using Shawn.Utils;

namespace PRM.Model.Protocol
{
    // ReSharper disable once InconsistentNaming
    public class SSH : ProtocolBaseWithAddressPortUserPwd, IKittyConnectable
    {
        public static string ProtocolName = "SSH";
        public SSH() : base(SSH.ProtocolName, $"Putty.{SSH.ProtocolName}.V1", SSH.ProtocolName)
        {
            base.UserName = "root";
            base.Port = "22";
        }

        [OtherName(Name = "PRM_SESSION_NAME")]
        public string SessionName => this.GetSessionName();

        private string _privateKey = "";

        [OtherName(Name = "PRM_SSH_PRIVATE_KEY_PATH")]
        public string PrivateKey
        {
            get => _privateKey;
            set => SetAndNotifyIfChanged(ref _privateKey, value);
        }

        private int? _sshVersion = 2;

        [OtherName(Name = "PRM_SSH_VERSION")]
        public int? SshVersion
        {
            get => _sshVersion;
            set => SetAndNotifyIfChanged(ref _sshVersion, value);
        }

        private string _startupAutoCommand = "";

        [OtherName(Name = "PRM_STARTUP_AUTO_COMMAND")]
        public string StartupAutoCommand
        {
            get => _startupAutoCommand;
            set => SetAndNotifyIfChanged(ref _startupAutoCommand, value);
        }

        private string _externalKittySessionConfigPath = "";
        public string ExternalKittySessionConfigPath
        {
            get => _externalKittySessionConfigPath;
            set => SetAndNotifyIfChanged(ref _externalKittySessionConfigPath, value);
        }


        private bool _openSftpOnConnected = false;
        public bool OpenSftpOnConnected
        {
            get => _openSftpOnConnected;
            set => SetAndNotifyIfChanged(ref _openSftpOnConnected, value);
        }

        public override bool IsOnlyOneInstance()
        {
            return false;
        }

        public override ProtocolBase? CreateFromJsonString(string jsonString)
        {
            try
            {
                var ret = JsonConvert.DeserializeObject<SSH>(jsonString);
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
            var ssh = (this.Clone() as SSH)!;
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
        public ProtocolBase ProtocolBase => this;

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