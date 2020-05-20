using System;
using System.Security;
using System.Text;
using System.Windows;
using Newtonsoft.Json;
using PRM.Core.Model;
using RdpHelper;
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
        /// <summary>
        /// Auto judge(by connection speed)
        /// </summary>
        Auto = 0,
        /// <summary>
        /// Low(8bit color with no feature support)
        /// </summary>
        Low = 1,
        /// <summary>
        /// Mdiddle(16bit color with only font smoothing and desktop composition)
        /// </summary>
        Middle = 2,
        /// <summary>
        /// High(32bit color with full features support)
        /// </summary>
        High = 3,
    }


    public class ProtocolServerRDP : ProtocolServerWithAddrPortUserPwdBase
    {
        public class LocalSetting : NotifyPropertyChangedBase
        {
            private bool _fullScreenLastSessionIsFullScreen = false;
            public bool FullScreenLastSessionIsFullScreen
            {
                get => _fullScreenLastSessionIsFullScreen;
                set => SetAndNotifyIfChanged(nameof(FullScreenLastSessionIsFullScreen), ref _fullScreenLastSessionIsFullScreen, value);
            }

            private int _fullScreenLastSessionScreenIndex = -1;
            public int FullScreenLastSessionScreenIndex
            {
                get => _fullScreenLastSessionScreenIndex;
                set => SetAndNotifyIfChanged(nameof(FullScreenLastSessionScreenIndex), ref _fullScreenLastSessionScreenIndex, value);
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

        public RdpConfig ToRdpConfig()
        {
            var rdp = new RdpConfig($"{this.Address}:{this.Port}", this.UserName, this.Password);
            rdp.AuthenticationLevel = 0;
            rdp.DisplayConnectionBar = 1;
            switch (this.RdpFullScreenFlag)
            {
                case ERdpFullScreenFlag.Disable:
                    rdp.ScreenModeId = 1;
                    rdp.DesktopWidth = this.RdpWidth;
                    rdp.DesktopHeight = this.RdpHeight;
                    break;
                case ERdpFullScreenFlag.EnableFullScreen:
                    rdp.ScreenModeId = 2;
                    break;
                case ERdpFullScreenFlag.EnableFullAllScreens:
                    rdp.ScreenModeId = 2;
                    rdp.UseMultimon = 1;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            switch (this.RdpWindowResizeMode)
            {
                case ERdpWindowResizeMode.AutoResize:
                    rdp.SmartSizing = 0;
                    rdp.DynamicResolution = 1;
                    break;
                case ERdpWindowResizeMode.Stretch:
                case ERdpWindowResizeMode.StretchFullScreen:
                    rdp.SmartSizing = 1;
                    rdp.DynamicResolution = 0;
                    break;
                case ERdpWindowResizeMode.Fixed:
                case ERdpWindowResizeMode.FixedFullScreen:
                    rdp.SmartSizing = 0;
                    rdp.DynamicResolution = 0;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }


            rdp.NetworkAutodetect = 0;
            switch (this.DisplayPerformance)
            {
                case EDisplayPerformance.Auto:
                    rdp.NetworkAutodetect = 1;
                    break;
                case EDisplayPerformance.Low:
                    rdp.ConnectionType = 1;
                    rdp.SessionBpp = 8;
                    rdp.AllowDesktopComposition = 0;
                    rdp.AllowFontSmoothing = 0;
                    rdp.DisableFullWindowDrag = 1;
                    rdp.DisableThemes = 1;
                    rdp.DisableWallpaper = 1;
                    rdp.DisableMenuAnims = 1;
                    rdp.DisableCursorSetting = 1;
                    break;
                case EDisplayPerformance.Middle:
                    rdp.SessionBpp = 16;
                    rdp.ConnectionType = 3;
                    rdp.AllowDesktopComposition = 1;
                    rdp.AllowFontSmoothing = 1;
                    rdp.DisableFullWindowDrag = 1;
                    rdp.DisableThemes = 1;
                    rdp.DisableWallpaper = 1;
                    rdp.DisableMenuAnims = 1;
                    rdp.DisableCursorSetting = 1;
                    break;
                case EDisplayPerformance.High:
                    rdp.SessionBpp = 32;
                    rdp.ConnectionType = 6;
                    rdp.AllowDesktopComposition = 1;
                    rdp.AllowFontSmoothing = 1;
                    rdp.DisableFullWindowDrag = 0;
                    rdp.DisableThemes = 0;
                    rdp.DisableWallpaper = 0;
                    rdp.DisableMenuAnims = 0;
                    rdp.DisableCursorSetting = 0;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            rdp.KeyboardHook = 0;
            rdp.AudioMode = 2;
            rdp.AudioCaptureMode = 0;

            
            if (this.EnableDiskDrives)
                rdp.RedirectPosDevices = 1;
            if (this.EnableClipboard)
                rdp.RedirectClipboard = 1;
            if (this.EnablePrinters)
                rdp.RedirectPrinters = 1;
            if (this.EnablePorts)
                rdp.RedirectComPorts = 1;
            if (this.EnableSmartCardsAndWinHello)
                rdp.RedirectSmartCards = 1;
            if (this.EnableKeyCombinations)
                rdp.KeyboardHook = 2;
            if (this.EnableSounds)
                rdp.AudioMode = 0;
            if (this.EnableAudioCapture)
                rdp.AudioCaptureMode = 1;

            rdp.AutoReconnectionEnabled = 1;

            return rdp;
        }
    }
}
