using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using PRM.Core.Ulits;
using Shawn.Ulits;

namespace PRM.Core.Model
{
    public enum EnumServerOrderBy
    {
        Name,
        AddTimeAsc,
        AddTimeDesc,
        Protocol,
    }
    public enum EnumTabMode
    {
        NewItemGoesToLatestActivate,
        NewItemGoesToGroup,
        NewItemGoesToProtocol,
    }
    public sealed class SystemConfigGeneral : SystemConfigBase
    {
        public SystemConfigGeneral(Ini ini) : base(ini)
        {
            Load();
            var appDateFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), SystemConfig.AppName);
            if (!Directory.Exists(appDateFolder))
                Directory.CreateDirectory(appDateFolder);
            IconFolderPath = Path.Combine(appDateFolder, "icons");
            if (!Directory.Exists(IconFolderPath))
                Directory.CreateDirectory(IconFolderPath);
            LogFilePath = Path.Combine(appDateFolder, SystemConfig.AppName + ".log.md");
        }

        private bool _appStartAutomatically = false;
        public bool AppStartAutomatically
        {
            get => _appStartAutomatically;
            set
            {
                SetAndNotifyIfChanged(nameof(AppStartAutomatically), ref _appStartAutomatically, value);
                if (value == true)
                {
                    SetSelfStartingHelper.SetSelfStart();
                }
                else
                {
                    SetSelfStartingHelper.UnsetSelfStart();
                }
            }
        }

        private bool _appStartMinimized = false;
        public bool AppStartMinimized
        {
            get => _appStartMinimized;
            set => SetAndNotifyIfChanged(nameof(AppStartMinimized), ref _appStartMinimized, value);
        }

        private string _iconFolderPath = "./Icons";
        public string IconFolderPath
        {
            get => _iconFolderPath;
            private set => SetAndNotifyIfChanged(nameof(IconFolderPath), ref _iconFolderPath, value);
        }

        private string _logFilePath = $"./{SystemConfig.AppName}.log.md";
        public string LogFilePath
        {
            get => _logFilePath;
            private set
            {
                SetAndNotifyIfChanged(nameof(LogFilePath), ref _logFilePath, value);
                SimpleLogHelper.LogFileName = value;
            }
        }

        private EnumServerOrderBy _serverOrderBy = EnumServerOrderBy.Name;
        public EnumServerOrderBy ServerOrderBy
        {
            get => _serverOrderBy;
            set => SetAndNotifyIfChanged(nameof(ServerOrderBy), ref _serverOrderBy, value);
        }
        

        private EnumTabMode _tabMode = EnumTabMode.NewItemGoesToLatestActivate;
        public EnumTabMode TabMode
        {
            get => _tabMode;
            set => SetAndNotifyIfChanged(nameof(TabMode), ref _tabMode, value);
        }


        #region Interface
        private const string _sectionName = "General";
        public override void Save()
        {
            StopAutoSave = true;
            _ini.WriteValue(nameof(AppStartAutomatically).ToLower(), _sectionName, AppStartAutomatically.ToString());
            _ini.WriteValue(nameof(AppStartMinimized).ToLower(), _sectionName, AppStartMinimized.ToString());
            _ini.WriteValue(nameof(ServerOrderBy).ToLower(), _sectionName, ServerOrderBy.ToString());
            _ini.WriteValue(nameof(TabMode).ToLower(), _sectionName, TabMode.ToString());
            StopAutoSave = false;
            _ini.Save();
        }

        public override void Load()
        {
            StopAutoSave = true;
            AppStartAutomatically = _ini.GetValue(nameof(AppStartAutomatically).ToLower(), _sectionName, AppStartAutomatically);
            AppStartMinimized = _ini.GetValue(nameof(AppStartMinimized).ToLower(), _sectionName, AppStartMinimized);
            if (Enum.TryParse<EnumServerOrderBy>(_ini.GetValue(nameof(ServerOrderBy).ToLower(), _sectionName, ServerOrderBy.ToString()), out var so))
                ServerOrderBy = so;
            if (Enum.TryParse<EnumTabMode>(_ini.GetValue(nameof(TabMode).ToLower(), _sectionName, TabMode.ToString()), out var tm))
                TabMode = tm;
            StopAutoSave = false;
        }

        public override void Update(SystemConfigBase newConfig)
        {
            UpdateBase(this, newConfig, typeof(SystemConfigGeneral));
        }

        #endregion
    }
}
