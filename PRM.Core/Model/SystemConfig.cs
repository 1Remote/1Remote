using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Shawn.Ulits;

namespace PRM.Core.Model
{
    public partial class SystemConfig : NotifyPropertyChangedBase
    {
        #region singleton
        private static SystemConfig uniqueInstance;
        private static readonly object InstanceLock = new object();
        public static SystemConfig GetInstance()
        {

            if (uniqueInstance == null)
            {
                throw new NullReferenceException("SystemConfig has not been inited!");
            }
            return uniqueInstance;
        }
        #endregion



        /// <summary>
        /// Must init before app start in app.cs
        /// </summary>
        public static void Init(ResourceDictionary appResourceDictionary)
        {
            if (uniqueInstance == null)
                lock (InstanceLock)
                {
                    if (uniqueInstance == null)
                    {
                        uniqueInstance = new SystemConfig(appResourceDictionary);
                    }
                }
        }



        public readonly string AppName = "PRemoteM";
        public readonly Ini Ini;
        private SystemConfig(ResourceDictionary appResourceDictionary)
        {
            Ini = new Ini(AppName + ".ini");
            Language = new SystemConfigLanguage(appResourceDictionary, Ini);
            General = new SystemConfigGeneral(Ini);
        }





        private SystemConfigLanguage _language = null;
        public SystemConfigLanguage Language
        {
            get => _language;
            protected set => SetAndNotifyIfChanged(nameof(Language), ref _language, value);
        }



        private SystemConfigGeneral _general = null;
        public SystemConfigGeneral General
        {
            get => _general;
            set => SetAndNotifyIfChanged(nameof(General), ref _general, value);
        }
    }


    public abstract class SystemConfigBase : NotifyPropertyChangedBase
    {
        protected Ini _ini = null;

        protected SystemConfigBase(Ini ini)
        {
            Debug.Assert(ini != null);
            _ini = ini;
        }


        public abstract void Save();
        public abstract void Load();
        public abstract void Update(SystemConfigBase newConfig);

        protected static void UpdateBase(SystemConfigBase old, SystemConfigBase newConfig, Type configType)
        {
            var t = configType;
            var fields = t.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            foreach (var fi in fields)
            {
                fi.SetValue(old, fi.GetValue(newConfig));
            }
            var properties = t.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
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
