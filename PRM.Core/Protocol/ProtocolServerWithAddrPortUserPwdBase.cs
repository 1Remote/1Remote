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
    public abstract class ProtocolServerWithAddrPortUserPwdBase : ProtocolServerBase
    {
        protected ProtocolServerWithAddrPortUserPwdBase(string protocol, string classVersion, string protocolDisplayName) : base(protocol, classVersion, protocolDisplayName)
        {
        }

        #region Conn

        private string _address = "";
        public string Address
        {
            get => _address;
            set => SetAndNotifyIfChanged(nameof(Address), ref _address, value);
        }


        public int GetPort()
        {
            if (int.TryParse(Port, out var p))
                return p;
            return 0;
        }
        private string _port = "3389";
        public string Port
        {
            get => !string.IsNullOrEmpty(_port) ? _port : "3389";
            set
            {
                if (int.TryParse(value, out var p))
                {
                    if (p > 0 && p < 65536)
                    {
                        SetAndNotifyIfChanged(nameof(Port), ref _port, value);
                        return;
                    }
                }
                SetAndNotifyIfChanged(nameof(Port), ref _port, "0");
                throw new ArgumentException("Port value error");
            }
        }


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


        protected override string GetSubTitle()
        {
            return $"@{Address}:{Port} ({UserName})";
        }
        #endregion
    }
}
