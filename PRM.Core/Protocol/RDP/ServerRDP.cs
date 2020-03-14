using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows;
using Newtonsoft.Json;
using PRM.Core.Base;

namespace PRM.RDP
{
    public class ServerRDP : ServerAbstract
    {
        public ServerRDP() : base("RDP", "RDP.V1")
        {
        }

        public enum EDisplayMode
        {
            FullScreen = 1,
            Window = 2,
        }
        public enum ERdpResizeMode
        {
            Fixed = 1,
            Sizable = 2,
        }
        public enum EDisplaySize
        {
            CustomSize = 0,
            UseCurrentMonitor = 1,
            UseMultiMonitors = 2,
        }



        private string _address;
        public string Address
        {
            get => _address;
            set => SetAndNotifyIfChanged(nameof(Address), ref _address, value);
        }


        private int _port = 0;
        public int Port
        {
            get => _port > 0 ? _port : 3389;
            set => SetAndNotifyIfChanged(nameof(Port), ref _port, value);
        }


        private string _userName;
        public string UserName
        {
            get => _userName;
            set => SetAndNotifyIfChanged(nameof(UserName), ref _userName, value);
        }


        private string _password;
        public string Password
        {
            get => _password;
            set => SetAndNotifyIfChanged(nameof(Password), ref _password, value);
        }


        private EDisplayMode _displayMode;
        public EDisplayMode DisplayMode
        {
            get => _displayMode;
            set => SetAndNotifyIfChanged(nameof(DisplayMode), ref _displayMode, value);
        }


        private EDisplaySize _rdpDisplaySize;
        public EDisplaySize RdpDisplaySize
        {
            get => _rdpDisplaySize;
            set => SetAndNotifyIfChanged(nameof(RdpDisplaySize), ref _rdpDisplaySize, value);
        }


        private int _rdpWidth;
        public int RdpWidth
        {
            get => _rdpWidth;
            set => SetAndNotifyIfChanged(nameof(RdpWidth), ref _rdpWidth, value);
        }


        private int _rdpHeight;
        public int RdpHeight
        {
            get => _rdpHeight;
            set => SetAndNotifyIfChanged(nameof(RdpHeight), ref _rdpHeight, value);
        }


        private ERdpResizeMode _rdpResizeMode;
        public ERdpResizeMode RdpResizeMode
        {
            get => _rdpResizeMode;
            set => SetAndNotifyIfChanged(nameof(RdpResizeMode), ref _rdpResizeMode, value);
        }


        private string _enableAudio;
        public string EnableAudio
        {
            get => _enableAudio;
            set => SetAndNotifyIfChanged(nameof(EnableAudio), ref _enableAudio, value);
        }


        public override void Conn()
        {
            if (string.IsNullOrEmpty(this.Address)
                || string.IsNullOrEmpty(this.UserName))
            {
                MessageBox.Show("Conn config err");
                return;
            }

            LassConnTime = DateTime.Now;

            var jsonstr = GetConfigJsonString();
            var jsonstrbase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonstr));
            System.Diagnostics.Process exep = new System.Diagnostics.Process();
            exep.StartInfo.FileName = "RdpRunner.exe";
            exep.StartInfo.Arguments = jsonstrbase64;
            exep.StartInfo.CreateNoWindow = true;
            exep.StartInfo.UseShellExecute = false;
            exep.Start();
        }


        public override string GetConfigJsonString()
        {
            return JsonConvert.SerializeObject(this);
        }


        public override ServerAbstract CreateFromJsonString(string jsonString)
        {
            try
            {
                return JsonConvert.DeserializeObject<ServerRDP>(jsonString);
            }
            catch (Exception e)
            {
                return null;
            }
        }
    }
}
