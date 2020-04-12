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


        private uint _modifiers = (uint)GlobalHotkeyHooker.HotkeyModifiers.Alt;
        public uint Modifiers
        {
            get => _modifiers;
            set => SetAndNotifyIfChanged(nameof(Modifiers), ref _modifiers, value);
        }

        private Key _hotKey = Key.M;
        public Key HotKey
        {
            get => _hotKey;
            set => SetAndNotifyIfChanged(nameof(HotKey), ref _hotKey, value);
        }


        #region Interface
        private const string _sectionName = "General";
        public override void Save()
        {
            _ini.WriteValue(nameof(Enable).ToLower(), _sectionName, Enable.ToString());
            _ini.Save();
        }

        public override void Load()
        {
            Enable = _ini.GetValue(nameof(Enable).ToLower(), _sectionName, Enable);
        }

        public override void Update(SystemConfigBase newConfig)
        {
            UpdateBase(this, newConfig, typeof(SystemConfigQuickConnect));
        }

        #endregion
    }
}
