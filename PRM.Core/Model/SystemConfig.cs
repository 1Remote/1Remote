using System;
using System.IO;
using System.Reflection;
using System.Timers;
using System.Windows;
using Microsoft.Win32;
using Shawn.Utils;

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

        #endregion singleton

        /// <summary>
        /// Must init before app start in app.cs
        /// </summary>
        public static void Init()
        {
            if (uniqueInstance != null) return;
            lock (InstanceLock)
            {
                uniqueInstance ??= new SystemConfig();
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

        public SystemConfigLocality Locality { get; set; }
        public SystemConfigLanguage Language { get; set; }
        public SystemConfigGeneral General { get; set; }
        public SystemConfigKeywordMatch KeywordMatch { get; set; }
        public SystemConfigLauncher Launcher { get; set; }
        public SystemConfigDataSecurity DataSecurity { get; set; }
        public SystemConfigTheme Theme { get; set; }

        private bool _stopAutoSaveConfig;

        public bool StopAutoSaveConfig
        {
            get => _stopAutoSaveConfig;
            set
            {
                _stopAutoSaveConfig = value;

                General.StopAutoSave = value;
                Language.StopAutoSave = value;
                KeywordMatch.StopAutoSave = value;
                Launcher.StopAutoSave = value;
                DataSecurity.StopAutoSave = value;
                Theme.StopAutoSave = value;
            }
        }

        public void Save()
        {
            Language.Save();
            General.Save();
            KeywordMatch.Save();
            Launcher.Save();
            DataSecurity.Save();
            Theme.Save();
            Locality.Save();
        }
    }
}