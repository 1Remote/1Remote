using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Shawn.Utils.Wpf;

namespace PRM.Service
{
    public enum HotkeyModifierKeys
    {
        [Description("None")]
        None = ModifierKeys.None,

        [Description("Control")]
        Control = ModifierKeys.Control,

        [Description("Shift")]
        Shift = ModifierKeys.Shift,

        [Description("Alt")]
        Alt = ModifierKeys.Alt,

        [Description("Win")]
        Windows = ModifierKeys.Windows,

        [Description("Shift + Control")]
        ShiftControl = ModifierKeys.Shift | ModifierKeys.Control,

        [Description("Shift + Win")]
        ShiftWindows = ModifierKeys.Shift | ModifierKeys.Windows,

        [Description("Shift + Alt")]
        ShiftAlt = ModifierKeys.Shift | ModifierKeys.Alt,

        [Description("Win + Control")]
        WindowsControl = ModifierKeys.Windows | ModifierKeys.Control,

        [Description("Win + Alt")]
        WindowsAlt = ModifierKeys.Windows | ModifierKeys.Alt,

        [Description("Control + Alt")]
        ControlAlt = ModifierKeys.Control | ModifierKeys.Alt,
    }

    public class LauncherService
    {

        private readonly LanguageService _languageService;

        public LauncherService(LanguageService languageService)
        {
            _languageService = languageService;
        }

        public bool CheckIfHotkeyAvailable(HotkeyModifierKeys modifier, Key key)
        {
            // check if HOTKEY_ALREADY_REGISTERED
            var r = GlobalHotkeyHooker.Instance.Register(null, (uint)modifier, key, () => { });
            switch (r.Item1)
            {
                case GlobalHotkeyHooker.RetCode.Success:
                    GlobalHotkeyHooker.Instance.Unregist(r.Item3);
                    return true;

                case GlobalHotkeyHooker.RetCode.ERROR_HOTKEY_NOT_REGISTERED:
                    MessageBox.Show(_languageService.Translate("hotkey_registered_fail") + ": " + r.Item2, _languageService.Translate("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                    break;

                case GlobalHotkeyHooker.RetCode.ERROR_HOTKEY_ALREADY_REGISTERED:
                    MessageBox.Show(_languageService.Translate("hotkey_already_registered") + ": " + r.Item2, _languageService.Translate("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(r.Item1.ToString());
            }

            return false;
        }

        public Action OnLauncherHotkeyPressed { get; set; }
    }
}
