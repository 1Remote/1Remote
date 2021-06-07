using System;
using System.IO;
using System.Threading.Tasks;
using Shawn.Utils;

namespace PRM.Core.Model
{
    public enum EnumServerOrderBy
    {
        IdAsc = -1,
        ProtocolAsc = 0,
        ProtocolDesc = 1,
        NameAsc = 2,
        NameDesc = 3,
        //TagAsc = 4,
        //TagDesc = 5,
        AddressAsc = 6,
        AddressDesc = 7,
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

        private EnumServerOrderBy _serverOrderBy = EnumServerOrderBy.NameAsc;

        public EnumServerOrderBy ServerOrderBy
        {
            get => _serverOrderBy;
            set => SetAndNotifyIfChanged(nameof(ServerOrderBy), ref _serverOrderBy, value);
        }

        #region Interface

        private const string _sectionName = "General";

        public override void Save()
        {
            StopAutoSave = true;

            _ini.WriteValue(nameof(AppStartAutomatically).ToLower(), _sectionName, AppStartAutomatically.ToString());
            _ini.WriteValue(nameof(AppStartMinimized).ToLower(), _sectionName, AppStartMinimized.ToString());
            _ini.WriteValue(nameof(ServerOrderBy).ToLower(), _sectionName, ServerOrderBy.ToString());

            SimpleLogHelper.Debug($"Set AppStartAutomatically = {AppStartAutomatically}");

#if FOR_MICROSOFT_STORE_ONLY
            SimpleLogHelper.Debug($"SetSelfStartingHelper.SetSelfStartByStartupTask({AppStartAutomatically}, \"{SystemConfig.AppName}\")");
            SetSelfStartingHelper.SetSelfStartByStartupTask(AppStartAutomatically, SystemConfig.AppName);
#else
            SimpleLogHelper.Debug($"SetSelfStartingHelper.SetSelfStartByRegistryKey({AppStartAutomatically}, \"{SystemConfig.AppName}\")");
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
            StopAutoSave = false;

#if FOR_MICROSOFT_STORE_ONLY
            Task.Factory.StartNew(async () =>
            {
                AppStartAutomatically = await SetSelfStartingHelper.IsSelfStartByStartupTask(SystemConfig.AppName);
            });
#endif
        }

        public override void Update(SystemConfigBase newConfig)
        {
            UpdateBase(this, newConfig, typeof(SystemConfigGeneral));
        }

        #endregion Interface
    }
}