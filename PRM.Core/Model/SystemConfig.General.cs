using System;
using System.IO;
using Shawn.Utils;

namespace PRM.Core.Model
{
    public enum EnumServerOrderBy
    {
        IdAsc = -1,
        Protocol = 0,
        ProtocolDesc = 1,
        Name = 2,
        NameDesc = 3,
        GroupName = 4,
        GroupNameDesc = 5,
        Address = 6,
        AddressDesc = 7,
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
            StopAutoSave = true;
            IconFolderPath = Path.Combine(appDateFolder, "icons");
            StopAutoSave = false;
        }

        private bool _appStartAutomatically = true;

        public bool AppStartAutomatically
        {
            get => _appStartAutomatically;
            set => SetAndNotifyIfChanged(nameof(AppStartAutomatically), ref _appStartAutomatically, value);
        }

        private bool _appStartMinimized = true;

        public bool AppStartMinimized
        {
            get => _appStartMinimized;
            set => SetAndNotifyIfChanged(nameof(AppStartMinimized), ref _appStartMinimized, value);
        }

        private string _iconFolderPath = "./icons";

        public string IconFolderPath
        {
            get => _iconFolderPath;
            private set => SetAndNotifyIfChanged(nameof(IconFolderPath), ref _iconFolderPath, value);
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

            // TODO delete after 2021.04;
            SetSelfStartingHelper.SetSelfStartByShortcut(false, SystemConfig.AppName);

#if FOR_MICROSOFT_STORE_ONLY
            SetSelfStartingHelper.SetSelfStartByStartupTask(AppStartAutomatically, SystemConfig.AppName);
#else
            SetSelfStartingHelper.SetSelfStartByRegistryKey(AppStartAutomatically, SystemConfig.AppName);
#endif

            StopAutoSave = false;
            _ini.Save();
        }

        public override void Load()
        {
            if (!_ini.ContainsKey(nameof(AppStartAutomatically).ToLower(), _sectionName))
                return;

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

        #endregion Interface
    }
}