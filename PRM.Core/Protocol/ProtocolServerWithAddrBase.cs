using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ColorPickerWPF.Code;
using Newtonsoft.Json;
using Shawn.Ulits;
using Brush = System.Drawing.Brush;
using Color = System.Windows.Media.Color;

namespace PRM.Core.Protocol
{
    public abstract class ProtocolServerWithAddrBase : ProtocolServerBase
    {
        protected ProtocolServerWithAddrBase(string protocol, string classVersion, string protocolDisplayName) : base(protocol, classVersion, protocolDisplayName)
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
            get => _password;
            set
            {
                // TODO 当输入为明文时，执行加密
                SetAndNotifyIfChanged(nameof(Password), ref _password, value);
            }
        }

        #endregion
    }
}
