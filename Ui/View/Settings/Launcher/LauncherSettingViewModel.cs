using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using _1RM.Model;
using _1RM.Service;
using Shawn.Utils;

namespace _1RM.View.Settings.Launcher
{
    public class LauncherSettingViewModel : NotifyPropertyChangedBase
    {
        private readonly ConfigurationService _configurationService;
        private readonly LauncherService _launcherService;

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
                    GlobalEventHelper.OnLauncherHotKeyChanged?.Invoke();
                }
            }
        }

        public HotkeyModifierKeys LauncherHotKeyModifiers
        {
            get => _configurationService.Launcher.HotKeyModifiers;
            set
            {
                if (value != LauncherHotKeyModifiers
                    && _launcherService.CheckIfHotkeyAvailable(value, LauncherHotKeyKey)
                    && SetAndNotifyIfChanged(ref _configurationService.Launcher.HotKeyModifiers, value))
                {
                    _configurationService.Save();
                    GlobalEventHelper.OnLauncherHotKeyChanged?.Invoke();
                }
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
                    _configurationService.Save();
                    GlobalEventHelper.OnLauncherHotKeyChanged?.Invoke();
                }
            }
        }
    }
}
