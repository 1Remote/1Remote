using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ColorPickerWPF.Code;
using Newtonsoft.Json;
using PRM.Core.Model;
using Shawn.Ulits;
using Brush = System.Drawing.Brush;
using Color = System.Windows.Media.Color;

namespace PRM.Core.Protocol
{
    public abstract class ProtocolServerWithAddrPortUserPwdBase : ProtocolServerWithAddrPortBase
    {
        protected ProtocolServerWithAddrPortUserPwdBase(string protocol, string classVersion, string protocolDisplayName, bool onlyOneInstance = true) : base(protocol, classVersion, protocolDisplayName, onlyOneInstance)
        {
        }

        #region Conn

        private string _userName = "Administrator";
        public string UserName
        {
            get => _userName;
            set => SetAndNotifyIfChanged(nameof(UserName), ref _userName, value);
        }

        private string _password = "";
        public string Password
        {
            get => _password;
            set => SetAndNotifyIfChanged(nameof(Password), ref _password, value);
        }

        public string GetDecryptedPassWord()
        {
            if (SystemConfig.Instance.DataSecurity.Rsa != null)
            {
                return SystemConfig.Instance.DataSecurity.Rsa.DecodeOrNull(_password) ?? "";
            }
            return _password;
        }

        protected override string GetSubTitle()
        {
            return $"@{Address}:{Port} ({UserName})";
        }
        #endregion
    }
}
