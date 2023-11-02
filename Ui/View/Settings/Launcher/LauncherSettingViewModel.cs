using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Service;
using MSTSCLib;
using Shawn.Utils;

namespace _1RM.View.Settings.Launcher
{
    public class LauncherSettingViewModel : NotifyPropertyChangedBase
    {
        private readonly ConfigurationService _configurationService;
        private readonly LauncherService _launcherService;

        public Dictionary<HotkeyModifierKeys, string> HotkeyModifierKeys => ConverterHotkeyModifierKeys.HotkeyModifierKeys;


        public LauncherSettingViewModel(ConfigurationService configurationService, LauncherService launcherService)
        {
            _configurationService = configurationService;
            _launcherService = launcherService;
        }

        public List<MatchProviderInfo> AvailableMatcherProviders => _configurationService.AvailableMatcherProviders;

        public bool LauncherEnabled
        {
            get => _configurationService.Launcher.LauncherEnabled;
            set
            {
                if (SetAndNotifyIfChanged(ref _configurationService.Launcher.LauncherEnabled, value))
                {
                    _configurationService.Save();
                    IoC.TryGet<LauncherWindowViewModel>()?.SetHotKey(LauncherEnabled, LauncherHotKeyModifiers, LauncherHotKeyKey);
                }
                RaisePropertyChanged();
            }
        }

        public HotkeyModifierKeys LauncherHotKeyModifiers
        {
            get => _configurationService.Launcher.HotKeyModifiers;
            set
            {
                if (value != LauncherHotKeyModifiers
                    && _launcherService.CheckIfHotkeyAvailable(value, LauncherHotKeyKey))
                {
                    _configurationService.Launcher.HotKeyModifiers = value;
                    if (false == IoC.TryGet<LauncherWindowViewModel>()?.SetHotKey(LauncherEnabled, LauncherHotKeyModifiers, LauncherHotKeyKey))
                    {
                        throw new ArgumentException();
                    }
                    _configurationService.Save();
                    RaisePropertyChanged(nameof(LauncherHotKeyStr));
                }
                RaisePropertyChanged();
            }
        }

        public Key LauncherHotKeyKey
        {
            get => _configurationService.Launcher.HotKeyKey;
            set
            {
                if (value != LauncherHotKeyKey
                    && _launcherService.CheckIfHotkeyAvailable(LauncherHotKeyModifiers, value)
                    && SetAndNotifyIfChanged(ref _configurationService.Launcher.HotKeyKey, value))
                {
                    if (false == IoC.TryGet<LauncherWindowViewModel>()?.SetHotKey(LauncherEnabled, LauncherHotKeyModifiers, LauncherHotKeyKey))
                    {
                        throw new ArgumentException();
                    }
                    _configurationService.Save();
                    RaisePropertyChanged(nameof(LauncherHotKeyStr));
                }
            }
        }

        public string LauncherHotKeyStr
        {
            get
            {
                if (HotkeyModifierKeys.ContainsKey(LauncherHotKeyModifiers))
                {
                    return HotkeyModifierKeys[LauncherHotKeyModifiers] + " + " + LauncherHotKeyKey;
                }
                return "Alt + M";
            }
        }


        public bool ShowCredentials
        {
            get => _configurationService.Launcher.ShowCredentials;
            set
            {
                if (SetAndNotifyIfChanged(ref _configurationService.Launcher.ShowCredentials, value))
                {
                    _configurationService.Save();
                }
            }
        }
    }



    public class ConverterHotkeyModifierKeys : IValueConverter
    {
        public static Dictionary<HotkeyModifierKeys, string> HotkeyModifierKeys = new Dictionary<HotkeyModifierKeys, string>
        {
            { _1RM.Service.HotkeyModifierKeys.Control, "Ctrl" },
            { _1RM.Service.HotkeyModifierKeys.Alt, "Alt" },
            { _1RM.Service.HotkeyModifierKeys.Windows, "Win" },
            { _1RM.Service.HotkeyModifierKeys.Shift, "Shift" },
            { _1RM.Service.HotkeyModifierKeys.ShiftAlt, "Shift + Alt" },
            { _1RM.Service.HotkeyModifierKeys.ShiftWindows, "Shift + Win" },
            { _1RM.Service.HotkeyModifierKeys.ShiftControl, "Shift + Ctrl" },
            { _1RM.Service.HotkeyModifierKeys.ControlAlt, "Ctrl + Alt" }
        };


        #region IValueConverter 成员  
        public object Convert(object? value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is not _1RM.Service.HotkeyModifierKeys mk || HotkeyModifierKeys.ContainsKey(mk) == false)
            {
                return 0;
            }

            return HotkeyModifierKeys.Keys.ToList().IndexOf(mk);
        }

        public object? ConvertBack(object? value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return null;
            return HotkeyModifierKeys.Keys.ToList()[int.Parse(value.ToString() ?? "0")];
        }
        #endregion
    }
}
