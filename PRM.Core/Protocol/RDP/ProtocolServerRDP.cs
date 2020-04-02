using System;
using System.Security;
using System.Text;
using System.Windows;
using Newtonsoft.Json;
using PRM.Core.Annotations;

namespace PRM.Core.Protocol.RDP
{
    public enum ERdpResizeMode
    {
        AutoResize = 0,
        Stretch = 1,
        Fixed = 2,
    }
    public enum EStartupDisplaySize
    {
        Window = 0,
        FullCurrentScreen = 1,
        FullAllScreens = 2,
    }

    public enum EDisplayPerformance
    {
        Auto = 0,
        Low = 1,
        Middle = 2,
        High = 3,
    }


    public class ProtocolServerRDP : ProtocolServerBase
    {
        public ProtocolServerRDP() : base("RDP", "RDP.V1")
        {
        }

        #region Conn
        private string _address;

        [NotNull]
        public string Address
        {
            get => _address;
            set
            {
                SetAndNotifyIfChanged(nameof(Address), ref _address, value);
            }
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

        // todo 使用 SecureString 保护密码
        private string _password;
        public string Password
        {
            get => _password;
            set => SetAndNotifyIfChanged(nameof(Password), ref _password, value);
        }
        #endregion


        #region Display

        private EStartupDisplaySize _rdpStartupDisplaySize;
        public EStartupDisplaySize RdpStartupDisplaySize
        {
            get => _rdpStartupDisplaySize;
            set => SetAndNotifyIfChanged(nameof(RdpStartupDisplaySize), ref _rdpStartupDisplaySize, value);
        }


        private ERdpResizeMode _rdpResizeMode;
        public ERdpResizeMode RdpResizeMode
        {
            get => _rdpResizeMode;
            set => SetAndNotifyIfChanged(nameof(RdpResizeMode), ref _rdpResizeMode, value);
        }


        private int _rdpWidth = 800;
        public int RdpWidth
        {
            get => _rdpWidth;
            set => SetAndNotifyIfChanged(nameof(RdpWidth), ref _rdpWidth, value);
        }


        private int _rdpHeight = 600;
        public int RdpHeight
        {
            get => _rdpHeight;
            set => SetAndNotifyIfChanged(nameof(RdpHeight), ref _rdpHeight, value);
        }



        private EDisplayPerformance _displayPerformance;
        public EDisplayPerformance DisplayPerformance
        {
            get => _displayPerformance;
            set => SetAndNotifyIfChanged(nameof(DisplayPerformance), ref _displayPerformance, value);
        }


        #endregion


        #region resource switch

        private bool _enableClipboard = true;
        public bool EnableClipboard
        {
            get => _enableClipboard;
            set => SetAndNotifyIfChanged(nameof(EnableClipboard), ref _enableClipboard, value);
        }



        private bool _enableDiskDrives = true;
        public bool EnableDiskDrives
        {
            get => _enableDiskDrives;
            set => SetAndNotifyIfChanged(nameof(EnableDiskDrives), ref _enableDiskDrives, value);
        }



        private bool _enableKeyCombinations = true;
        public bool EnableKeyCombinations
        {
            get => _enableKeyCombinations;
            set => SetAndNotifyIfChanged(nameof(EnableKeyCombinations), ref _enableKeyCombinations, value);
        }


        private bool _enableSounds = true;
        public bool EnableSounds
        {
            get => _enableSounds;
            set
            {
                if(value == true)
                    EnableClipboard = true;
                SetAndNotifyIfChanged(nameof(EnableSounds), ref _enableSounds, value);
            }
        }


        private bool _enableAudioCapture = false;
        public bool EnableAudioCapture
        {
            get => _enableAudioCapture;
            set => SetAndNotifyIfChanged(nameof(EnableAudioCapture), ref _enableAudioCapture, value);
        }





        private bool _enablePorts = false;
        public bool EnablePorts
        {
            get => _enablePorts;
            set => SetAndNotifyIfChanged(nameof(EnablePorts), ref _enablePorts, value);
        }




        private bool _enablePrinters = false;
        public bool EnablePrinters
        {
            get => _enablePrinters;
            set => SetAndNotifyIfChanged(nameof(EnablePrinters), ref _enablePrinters, value);
        }




        private bool _enableSmartCardsAndWinHello = false;
        public bool EnableSmartCardsAndWinHello
        {
            get => _enableSmartCardsAndWinHello;
            set => SetAndNotifyIfChanged(nameof(EnableSmartCardsAndWinHello), ref _enableSmartCardsAndWinHello, value);
        }

        #endregion



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


        public override ProtocolServerBase CreateFromJsonString(string jsonString)
        {
            try
            {
                return JsonConvert.DeserializeObject<ProtocolServerRDP>(jsonString);
            }
            catch (Exception e)
            {
                return null;
            }
        }

        protected override string GetSubTitle()
        {
            return Address + " @ " + UserName;
        }
    }
}
