using System.ComponentModel;
using System.Windows.Input;
using Shawn.Utils;

namespace PRM.Core.Model
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

    public sealed class SystemConfigLauncher : SystemConfigBase
    {
        public SystemConfigLauncher(Ini ini) : base(ini)
        {
            Load();
        }


        private bool _enable = true;
        public bool Enable
        {
            get => _enable;
            set => SetAndNotifyIfChanged(nameof(Enable), ref _enable, value);
        }


        private HotkeyModifierKeys _hotKeyModifiers = HotkeyModifierKeys.Alt;
        public HotkeyModifierKeys HotKeyModifiers
        {
            get => _hotKeyModifiers;
            set => SetAndNotifyIfChanged(nameof(HotKeyModifiers), ref _hotKeyModifiers, value);
        }

        private Key _hotKeyKey = Key.M;
        public Key HotKeyKey
        {
            get => _hotKeyKey;
            set => SetAndNotifyIfChanged(nameof(HotKeyKey), ref _hotKeyKey, value);
        }

        private bool _allowGroupNameSearch = true;
        public bool AllowGroupNameSearch
        {
            get => _allowGroupNameSearch;
            set => SetAndNotifyIfChanged(nameof(AllowGroupNameSearch), ref _allowGroupNameSearch, value);
        }


        #region Interface
        private const string _sectionName = "QuickConnect";
        public override void Save()
        {
            _ini.WriteValue(nameof(Enable).ToLower(), _sectionName, Enable.ToString());
            _ini.WriteValue(nameof(HotKeyModifiers).ToLower(), _sectionName, ((uint)HotKeyModifiers).ToString());
            _ini.WriteValue(nameof(HotKeyKey).ToLower(), _sectionName, ((uint)HotKeyKey).ToString());
            _ini.WriteValue(nameof(AllowGroupNameSearch).ToLower(), _sectionName, AllowGroupNameSearch.ToString());
            _ini.Save();
        }

        public override void Load()
        {
            if (!_ini.ContainsKey(nameof(Enable).ToLower(), _sectionName))
                return;

            StopAutoSave = true;
            Enable = _ini.GetValue(nameof(Enable).ToLower(), _sectionName, Enable);
            uint modifiers = 0;
            uint key = 0;
            modifiers = _ini.GetValue(nameof(HotKeyModifiers).ToLower(), _sectionName, modifiers);
            key = _ini.GetValue(nameof(HotKeyKey).ToLower(), _sectionName, key);
            HotKeyModifiers = (HotkeyModifierKeys)modifiers;
            HotKeyKey = (Key)key;
            if (HotKeyModifiers == HotkeyModifierKeys.None || HotKeyKey == Key.None)
            {
                HotKeyModifiers = HotkeyModifierKeys.Alt;
                HotKeyKey = Key.M;
            }
            AllowGroupNameSearch = _ini.GetValue(nameof(AllowGroupNameSearch).ToLower(), _sectionName, AllowGroupNameSearch);
            StopAutoSave = false;
        }

        public override void Update(SystemConfigBase newConfig)
        {
            UpdateBase(this, newConfig, typeof(SystemConfigLauncher));
        }

        #endregion
    }
}
