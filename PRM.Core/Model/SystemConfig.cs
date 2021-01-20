using System;
using System.Reflection;
using System.Timers;
using System.Windows;
using Shawn.Utils;
using Microsoft.Win32;

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
        public static void Init()
        {
            if (uniqueInstance == null)
                lock (InstanceLock)
                {
                    if (uniqueInstance == null)
                    {
                        uniqueInstance = new SystemConfig();
                    }
                }
        }
#if DEV
        public const string AppName = "PRemoteM_Debug";
        public const string AppFullName = "PersonalRemoteManager_Debug";
#else
        public const string AppName = "PRemoteM";
        public const string AppFullName = "PersonalRemoteManager";
#endif
        public static Ini Ini { get; set; }
        public static ResourceDictionary AppResourceDictionary { get; set; }


        private readonly Timer _checkUpdateTimer;

        private SystemConfig()
        {
            GlobalEventHelper.OnNewVersionRelease += (s, s1) =>
            {
                this.NewVersion = s;
                this.NewVersionUrl = s1;
            };


            _lastScreenCount = System.Windows.Forms.Screen.AllScreens.Length;
            _lastScreenRectangle = GetScreenSize();
            SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;

            // check update every hours.
            _checkUpdateTimer = new Timer()
            {
                Interval = 1000 * 3600,
                AutoReset = true,
            };
            _checkUpdateTimer.Elapsed += (sender, args) =>
            {
                var uc = new UpdateChecker(GlobalEventHelper.OnNewVersionRelease);
                uc.CheckUpdateAsync();
            };
            // check one time right now!
            {
                var uc = new UpdateChecker(GlobalEventHelper.OnNewVersionRelease);
                uc.CheckUpdateAsync();
            }
        }

        ~SystemConfig()
        {
            SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
            _checkUpdateTimer?.Stop();
        }

        #region Resolution Watcher
        private static int _lastScreenCount = 0;
        private static System.Drawing.Rectangle _lastScreenRectangle = System.Drawing.Rectangle.Empty;
        private void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e)
        {
            SimpleLogHelper.Debug("Resolution Change: " + e);
            var newScreenCount = System.Windows.Forms.Screen.AllScreens.Length;
            var newScreenRectangle = GetScreenSize();
            if (newScreenCount != _lastScreenCount
               || newScreenRectangle.Width != _lastScreenRectangle.Width
               || newScreenRectangle.Height != _lastScreenRectangle.Height)
                GlobalEventHelper.OnScreenResolutionChanged?.Invoke();
            _lastScreenCount = newScreenCount;
            _lastScreenRectangle = newScreenRectangle;
        }
        private static System.Drawing.Rectangle GetScreenSize()
        {
            var entireSize = System.Drawing.Rectangle.Empty;
            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
                entireSize = System.Drawing.Rectangle.Union(entireSize, screen.Bounds);
            return entireSize;
        }
        #endregion


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
            set => SetAndNotifyIfChanged(nameof(Language), ref _language, value);
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


        private bool _stopAutoSaveConfig;
        public bool StopAutoSaveConfig
        {
            get => _stopAutoSaveConfig;
            set
            {
                _stopAutoSaveConfig = value;

                General.StopAutoSave = value;
                Language.StopAutoSave = value;
                QuickConnect.StopAutoSave = value;
                DataSecurity.StopAutoSave = value;
                Theme.StopAutoSave = value;
            }
        }

        public void Save()
        {
            Language.Save();
            General.Save();
            QuickConnect.Save();
            DataSecurity.Save();
            Theme.Save();
        }
    }


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

        protected Ini _ini = null;

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
