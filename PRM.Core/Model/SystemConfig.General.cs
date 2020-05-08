using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
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
    public sealed class SystemConfigGeneral : SystemConfigBase
    {
        public SystemConfigGeneral(Ini ini) : base(ini)
        {
            Load();
        }

        private bool _appStartAutomatically = false;
        public bool AppStartAutomatically
        {
            get => _appStartAutomatically;
            set => SetAndNotifyIfChanged(nameof(AppStartAutomatically), ref _appStartAutomatically, value);
        }

        private bool _appStartMinimized = false;
        public bool AppStartMinimized
        {
            get => _appStartMinimized;
            set => SetAndNotifyIfChanged(nameof(AppStartMinimized), ref _appStartMinimized, value);
        }


        public string IconFolderPath => "./Icons";


        private EnumServerOrderBy _serverOrderBy = EnumServerOrderBy.Name;
        public EnumServerOrderBy ServerOrderBy
        {
            get => _serverOrderBy;
            set => SetAndNotifyIfChanged(nameof(ServerOrderBy), ref _serverOrderBy, value);
        }



        #region Interface
        private const string _sectionName = "General";
        public override void Save()
        {
            _ini.WriteValue(nameof(AppStartAutomatically).ToLower(), _sectionName, AppStartAutomatically.ToString());
            _ini.WriteValue(nameof(AppStartMinimized).ToLower(), _sectionName, AppStartMinimized.ToString());
            _ini.WriteValue(nameof(ServerOrderBy).ToLower(), _sectionName, ServerOrderBy.ToString());
            _ini.Save();
        }

        public override void Load()
        {
            AppStartAutomatically = _ini.GetValue(nameof(AppStartAutomatically).ToLower(), _sectionName, AppStartAutomatically);
            AppStartMinimized = _ini.GetValue(nameof(AppStartMinimized).ToLower(), _sectionName, AppStartMinimized);
            if (Enum.TryParse<EnumServerOrderBy>(_ini.GetValue(nameof(ServerOrderBy).ToLower(), _sectionName, ServerOrderBy.ToString()), out var so))
            {
                ServerOrderBy = so;
            }
        }

        public override void Update(SystemConfigBase newConfig)
        {
            UpdateBase(this, newConfig, typeof(SystemConfigGeneral));
        }

        #endregion
    }
}
