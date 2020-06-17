using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
        public static SystemConfig Instance => GetInstance();
        #endregion



        /// <summary>
        /// Must init before app start in app.cs
        /// </summary>
        public static void Create(ResourceDictionary appResourceDictionary)
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
#if DEBUG
        public const string AppName = "PRemoteM_Debug";
        public const string AppFullName = "PersonalRemoteManager_Debug";
#else
        public const string AppName = "PRemoteM";
        public const string AppFullName = "PersonalRemoteManager";
#endif
        public readonly Ini Ini;
        private SystemConfig(ResourceDictionary appResourceDictionary)
        {
            var appDateFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), SystemConfig.AppName);
            if (!Directory.Exists(appDateFolder))
                Directory.CreateDirectory(appDateFolder);
            var iniPath = Path.Combine(appDateFolder, AppName + ".ini");
            Ini = new Ini(iniPath);
            Language = new SystemConfigLanguage(appResourceDictionary, Ini);
            General = new SystemConfigGeneral(Ini);
            QuickConnect = new SystemConfigQuickConnect(Ini);
            DataSecurity = new SystemConfigDataSecurity(Ini);
            Theme = new SystemConfigTheme(Ini);
            SimpleLogHelper.LogFileName = General.LogFilePath;

            var uc = new UpdateChecker();
            uc.OnNewRelease += (s, s1) =>
            {
                this.NewVersion = s;
                this.NewVersionUrl = s1;
            };
            uc.CheckUpdateAsync();
        }

#region Update
        private string _newVersion = "";
        public string NewVersion
        {
            get => _newVersion;
            set => SetAndNotifyIfChanged(nameof(NewVersion), ref _newVersion, value);
        }

        private string _newVersionUrl = "";
        public string NewVersionUrl
        {
            get => _newVersionUrl;
            set => SetAndNotifyIfChanged(nameof(NewVersionUrl), ref _newVersionUrl, value);
        }
#endregion


        public SystemConfigLocality Locality = new SystemConfigLocality();


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



        private SystemConfigQuickConnect _quickConnect = null;
        public SystemConfigQuickConnect QuickConnect
        {
            get => _quickConnect;
            set => SetAndNotifyIfChanged(nameof(QuickConnect), ref _quickConnect, value);
        }




        private SystemConfigDataSecurity _dataSecurity = null;
        public SystemConfigDataSecurity DataSecurity
        {
            get => _dataSecurity;
            set => SetAndNotifyIfChanged(nameof(DataSecurity), ref _dataSecurity, value);
        }




        private SystemConfigTheme _theme = null;
        public SystemConfigTheme Theme
        {
            get => _theme;
            set => SetAndNotifyIfChanged(nameof(Theme), ref _theme, value);
        }
    }


    public abstract class SystemConfigBase : NotifyPropertyChangedBase
    {
        private object locker = new object();
        protected bool StopAutoSave
        {
            get => _stopAutoSave;
            set => _stopAutoSave = value;
        }

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

        protected Ini _ini = null;
        private bool _stopAutoSave = false;

        protected SystemConfigBase(Ini ini)
        {
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
