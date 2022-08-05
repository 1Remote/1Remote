using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using _1RM.Utils;
using Shawn.Utils.Wpf;

namespace _1RM.Service
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
#pragma warning disable CS8625
            var r = GlobalHotkeyHooker.Instance.Register(null, (uint)modifier, key, () => { });
#pragma warning restore CS8625
            switch (r.Item1)
            {
                case GlobalHotkeyHooker.RetCode.Success:
                    GlobalHotkeyHooker.Instance.Unregist(r.Item3);
                    return true;

                case GlobalHotkeyHooker.RetCode.ERROR_HOTKEY_NOT_REGISTERED:
                    MessageBoxHelper.ErrorAlert(_languageService.Translate("hotkey_registered_fail") + ": " + r.Item2);
                    break;

                case GlobalHotkeyHooker.RetCode.ERROR_HOTKEY_ALREADY_REGISTERED:
                    MessageBoxHelper.ErrorAlert(_languageService.Translate("hotkey_already_registered") + ": " + r.Item2);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(r.Item1.ToString());
            }

            return false;
        }

        public Action? OnLauncherHotkeyPressed { get; set; }
    }
}
