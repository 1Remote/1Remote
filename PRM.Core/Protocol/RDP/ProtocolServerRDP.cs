using System;
using System.Security;
using System.Text;
using System.Windows;
using Newtonsoft.Json;
using PRM.Core.Model;
using Shawn.Ulits.RDP;

namespace PRM.Core.Protocol.RDP
{
    public enum ERdpWindowResizeMode
    {
        AutoResize = 0,
        Stretch = 1,
        Fixed = 2,
    }
    public enum ERdpFullScreenFlag
    {
        Disable = 0,
        EnableFullScreen = 1,
        EnableFullAllScreens = 2,
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
        public class LocalSetting : NotifyPropertyChangedBase
        {
            private bool _fullScreenLastSessionIsFullScreen = true;
            public bool FullScreen_LastSessionIsFullScreen
            {
                get => _fullScreenLastSessionIsFullScreen;
                set => SetAndNotifyIfChanged(nameof(FullScreen_LastSessionIsFullScreen), ref _fullScreenLastSessionIsFullScreen, value);
            }

            private int _fullScreenLastSessionScreenIndex = -1;
            public int FullScreen_LastSessionScreenIndex
            {
                get => _fullScreenLastSessionScreenIndex;
                set => SetAndNotifyIfChanged(nameof(FullScreen_LastSessionScreenIndex), ref _fullScreenLastSessionScreenIndex, value);
            }
        }

        public ProtocolServerRDP() : base("RDP", "RDP.V1", "RDP")
        {
            UserName = "Administrator";
        }

        #region Conn
        private string _address;

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

        private string _password;
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


        #region Display

        private ERdpFullScreenFlag _rdpFullScreenFlag;
        public ERdpFullScreenFlag RdpFullScreenFlag
        {
            get => _rdpFullScreenFlag;
            set => SetAndNotifyIfChanged(nameof(RdpFullScreenFlag), ref _rdpFullScreenFlag, value);
        }


        private ERdpWindowResizeMode _rdpWindowResizeMode;
        public ERdpWindowResizeMode RdpWindowResizeMode
        {
            get => _rdpWindowResizeMode;
            set => SetAndNotifyIfChanged(nameof(RdpWindowResizeMode), ref _rdpWindowResizeMode, value);
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
            set
            {
                if (!value && _enableSounds)
                {
                    SetAndNotifyIfChanged(nameof(EnableSounds), ref _enableSounds, false);
                }
                SetAndNotifyIfChanged(nameof(EnableClipboard), ref _enableClipboard, value);
            }
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
                if (value && !_enableClipboard)
                {
                    SetAndNotifyIfChanged(nameof(EnableClipboard), ref _enableClipboard, true);
                }
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

        private LocalSetting _autoSetting = new LocalSetting();
        public LocalSetting AutoSetting
        {
            get => _autoSetting;
            set => SetAndNotifyIfChanged(nameof(AutoSetting), ref _autoSetting, value);
        }




        public override string ToJsonString()
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

        public override string GetSubTitle()
        {
            return Address + " @ " + UserName;
        }

    }
}
