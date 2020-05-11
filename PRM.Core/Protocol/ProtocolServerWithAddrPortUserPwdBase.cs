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
            set
            {
                SetAndNotifyIfChanged(nameof(Address), ref _address, value);
            }
        }


        private int _port = 3389;
        public int Port
        {
            get => _port > 0 ? _port : 3389;
            set => SetAndNotifyIfChanged(nameof(Port), ref _port, value);
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
            get
            {
                var rsa = SystemConfig.GetInstance().DataSecurity.Rsa;
                if (rsa != null)
                {
                    var tmp = rsa.DecodeOrNull(_password);
                    if (tmp != null)
                    {
                        return tmp;
                    }
                }
                return _password;
            }
            set
            {
                Debug.Assert(value != null);
                if (_password != value)
                {
                    var rsa = SystemConfig.GetInstance().DataSecurity.Rsa;
                    var pwd = value;
                    if (rsa != null)
                    {
                        var tmp = rsa.DecodeOrNull(value);
                        if (tmp == null)
                        {
                            pwd = rsa.Encode(value);
                        }
                    }
                    SetAndNotifyIfChanged(nameof(Password), ref _password, pwd);
                }
            }
        }


        protected override string GetSubTitle()
        {
            return $"@{Address}:{Port} ({UserName})";
        }
        #endregion
    }
}
