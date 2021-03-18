using System;
using System.Reflection;
using Shawn.Utils;

namespace PRM.Core.Model
{
    public abstract class SystemConfigBase : NotifyPropertyChangedBase
    {
        public bool StopAutoSave { get; set; } = false;

        protected override void SetAndNotifyIfChanged<T>(string propertyName, ref T oldValue, T newValue)
        {
            if (oldValue == null && newValue == null) return;
            if (oldValue != null && oldValue.Equals(newValue)) return;
            if (newValue != null && newValue.Equals(oldValue)) return;
            oldValue = newValue;
            RaisePropertyChanged(propertyName);
            if (!StopAutoSave)
                Save();
        }

        private protected Ini _ini = null;

        protected SystemConfigBase(Ini ini)
        {
            _ini = ini;
        }

        public abstract void Save();

        public abstract void Load();

        public abstract void Update(SystemConfigBase newConfig);

        protected static void UpdateBase(SystemConfigBase old, SystemConfigBase newConfig, Type configType)
        {
            var fields = configType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            foreach (var fi in fields)
            {
                fi.SetValue(old, fi.GetValue(newConfig));
            }
            var properties = configType.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            foreach (var property in properties)
            {
                if (property.SetMethod != null)
                {
                    // update properties without notify
                    property.SetValue(old, property.GetValue(newConfig));
                    // then raise notify
                    old.RaisePropertyChanged(property.Name);
                }
            }
        }
    }
}