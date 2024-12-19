﻿using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using _1RM.Model.Protocol.Base;
using _1RM.Utils.KiTTY;
using Shawn.Utils;
using System.Text.RegularExpressions;
using System.Windows.Controls;

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
            // https://github.com/kovidgoyal/kitty
            return 2;
        }

        [JsonIgnore]
        public ProtocolBase ProtocolBase => this;

        private static bool ValidateIPv6(string ipAddress)
        {
            string pattern = @"^([\da-fA-F]{1,4}:){7}[\da-fA-F]{1,4}$";
            Regex regex = new Regex(pattern);

            return regex.IsMatch(ipAddress);
        }


        public string GetExeArguments(string sessionName)
        {
            var ssh = (this.Clone() as SSH)!;
            ssh.ConnectPreprocess();

            //var arg = $@" -load ""{ssh.SessionName}"" {ssh.Address} -P {ssh.Port} -l {ssh.UserName} -pw {ssh.Password} -{(int)(ssh.SshVersion ?? 2)} -cmd ""{ssh.StartupAutoCommand}""";

            //var template = $@" -load ""{this.GetSessionName()}"" %1RM_HOSTNAME% -P %1RM_PORT% -l %1RM_USERNAME% -pw %1RM_PASSWORD% -%SSH_VERSION% -cmd ""%STARTUP_AUTO_COMMAND%""";
            //var arg = OtherNameAttributeExtensions.Replace(ssh, template);

            var ipv6 = ValidateIPv6(ssh.Address) ? " -6 " : "";
            var arg = $@" -load ""{sessionName}"" {ssh.Address} -P {ssh.Port} -l {ssh.UserName} -pw {ssh.Password} -{(int)(ssh.SshVersion ?? 2)} -cmd ""{ssh.StartupAutoCommand}"" {ipv6}";
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