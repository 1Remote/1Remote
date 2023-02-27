using System;
using Newtonsoft.Json;
using _1RM.Model.Protocol.Base;
using _1RM.Utils.KiTTY;
using Shawn.Utils;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using System.Runtime.Intrinsics.X86;

namespace _1RM.Model.Protocol
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

        private string _privateKey = "";
        [OtherName(Name = "SSH_PRIVATE_KEY_PATH")]
        public string PrivateKey
        {
            get => _privateKey;
            set => SetAndNotifyIfChanged(ref _privateKey, value);
        }

        private int? _sshVersion = 2;

        [OtherName(Name = "SSH_VERSION")]
        public int? SshVersion
        {
            get => _sshVersion;
            set => SetAndNotifyIfChanged(ref _sshVersion, value);
        }

        private string _startupAutoCommand = "";

        [OtherName(Name = "STARTUP_AUTO_COMMAND")]
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

        [JsonIgnore]
        public ProtocolBase ProtocolBase => this;

        public string GetExeArguments(string sessionName)
        {
            var ssh = (this.Clone() as SSH)!;
            ssh.ConnectPreprocess();

            //var arg = $@" -load ""{ssh.SessionName}"" {ssh.Address} -P {ssh.Port} -l {ssh.UserName} -pw {ssh.Password} -{(int)(ssh.SshVersion ?? 2)} -cmd ""{ssh.StartupAutoCommand}""";

            //var template = $@" -load ""{this.GetSessionName()}"" %HOSTNAME% -P %PORT% -l %USERNAME% -pw %PASSWORD% -%SSH_VERSION% -cmd ""%STARTUP_AUTO_COMMAND%""";
            //var arg = OtherNameAttributeExtensions.Replace(ssh, template);

            var arg = $@" -load ""{sessionName}"" {ssh.Address} -P {ssh.Port} -l {ssh.UserName} -pw {ssh.Password} -{(int)(ssh.SshVersion ?? 2)} -cmd ""{ssh.StartupAutoCommand}""";
            return " " + arg;
        }

        public override void ConnectPreprocess()
        {
            base.ConnectPreprocess();
            StartupAutoCommand = StartupAutoCommand.Replace(@"""", @"\""");
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
    }
}