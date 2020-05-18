using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Shawn.Ulits;

namespace PRM.Core.Model
{
    public sealed class SystemConfigQuickConnect : SystemConfigBase
    {
        public SystemConfigQuickConnect(Ini ini) : base(ini)
        {
            Load();
        }


        private bool _enable = true;
        public bool Enable
        {
            get => _enable;
            set => SetAndNotifyIfChanged(nameof(Enable), ref _enable, value);
        }


        private ModifierKeys _hotKeyModifiers = ModifierKeys.Alt;
        public ModifierKeys HotKeyModifiers
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


        #region Interface
        private const string _sectionName = "QuickConnect";
        public override void Save()
        {
            _ini.WriteValue(nameof(Enable).ToLower(), _sectionName, Enable.ToString());
            _ini.WriteValue(nameof(HotKeyModifiers).ToLower(), _sectionName, ((uint)HotKeyModifiers).ToString());
            _ini.WriteValue(nameof(HotKeyKey).ToLower(), _sectionName, ((uint)HotKeyKey).ToString());
            _ini.Save();
        }

        public override void Load()
        {
            StopAutoSave = true;
            Enable = _ini.GetValue(nameof(Enable).ToLower(), _sectionName, Enable);
            uint modifiers = 0;
            uint key = 0;
            modifiers = _ini.GetValue(nameof(HotKeyModifiers).ToLower(), _sectionName, modifiers);
            key = _ini.GetValue(nameof(HotKeyKey).ToLower(), _sectionName, key);
            HotKeyModifiers = (ModifierKeys)modifiers;
            HotKeyKey = (Key)key;
            if (HotKeyModifiers == ModifierKeys.None || HotKeyKey == Key.None)
            {
                HotKeyModifiers = ModifierKeys.Alt;
                HotKeyKey = Key.M;
            }
            StopAutoSave = false;
        }

        public override void Update(SystemConfigBase newConfig)
        {
            UpdateBase(this, newConfig, typeof(SystemConfigQuickConnect));
        }

        #endregion
    }
}
