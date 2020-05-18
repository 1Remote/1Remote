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
        StretchFullScreen = 3,
        FixedFullScreen = 4,
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


    public class ProtocolServerRDP : ProtocolServerWithAddrPortUserPwdBase
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
            base.Port = "3389";
            base.UserName = "Administrator";
        }


        #region Display

        private ERdpFullScreenFlag _rdpFullScreenFlag = ERdpFullScreenFlag.EnableFullScreen;
        public ERdpFullScreenFlag RdpFullScreenFlag
        {
            get => _rdpFullScreenFlag;
            set
            {
                SetAndNotifyIfChanged(nameof(RdpFullScreenFlag), ref _rdpFullScreenFlag, value);
                switch (value)
                {
                    case ERdpFullScreenFlag.EnableFullAllScreens:
                        IsConnWithFullScreen = true;
                        break;
                    case ERdpFullScreenFlag.EnableFullScreen:
                        break;
                    case ERdpFullScreenFlag.Disable:
                        IsConnWithFullScreen = false;
                        if(RdpWindowResizeMode == ERdpWindowResizeMode.FixedFullScreen)
                            RdpWindowResizeMode = ERdpWindowResizeMode.Fixed;
                        if(RdpWindowResizeMode == ERdpWindowResizeMode.StretchFullScreen)
                            RdpWindowResizeMode = ERdpWindowResizeMode.Stretch;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(value), value, null);
                }
            }
        }


        private bool _isConnWithFullScreen = false;
        public bool IsConnWithFullScreen
        {
            get => _isConnWithFullScreen;
            set => SetAndNotifyIfChanged(nameof(IsConnWithFullScreen), ref _isConnWithFullScreen, value);
        }

        private ERdpWindowResizeMode _rdpWindowResizeMode = ERdpWindowResizeMode.AutoResize;
        public ERdpWindowResizeMode RdpWindowResizeMode
        {
            get => _rdpWindowResizeMode;
            set
            {
                var tmp = value;
                if (RdpFullScreenFlag == ERdpFullScreenFlag.Disable)
                {
                    if (tmp == ERdpWindowResizeMode.FixedFullScreen)
                        tmp = ERdpWindowResizeMode.Fixed;
                    if (tmp == ERdpWindowResizeMode.StretchFullScreen)
                        tmp = ERdpWindowResizeMode.Stretch;
                }
                _rdpWindowResizeMode = tmp;
                RaisePropertyChanged(nameof(RdpWindowResizeMode));
            }
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



        private EDisplayPerformance _displayPerformance = EDisplayPerformance.Auto;
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
    }
}
