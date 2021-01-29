using System;
using System.IO;
using System.Windows;
using Shawn.Utils;

namespace PRM.Core.Model
{
    public sealed class SystemConfigLocality : SystemConfigBase
    {
        public SystemConfigLocality(Ini ini) : base(ini)
        {
            var appDateFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), SystemConfig.AppName);
            if (!Directory.Exists(appDateFolder))
                Directory.CreateDirectory(appDateFolder);
            Load();
        }


        public string MainWindowTabSelected = "";
        public double MainWindowWidth = 680;
        public double MainWindowHeight = 530;
        public double TabWindowWidth = 800;
        public double TabWindowHeight = 600;

        private WindowState _tabWindowState = WindowState.Normal;
        public WindowState TabWindowState
        {
            get => _tabWindowState;
            set
            {
                if (value != WindowState.Minimized)
                    SetAndNotifyIfChanged(nameof(TabWindowState), ref _tabWindowState, value);
            }
        }


        #region Interface
        private const string _sectionName = "Locality";
        public override void Save()
        {
            _ini.WriteValue(nameof(MainWindowWidth).ToLower(), _sectionName, MainWindowWidth.ToString());
            _ini.WriteValue(nameof(MainWindowHeight).ToLower(), _sectionName, MainWindowHeight.ToString());
            _ini.WriteValue(nameof(TabWindowWidth).ToLower(), _sectionName, TabWindowWidth.ToString());
            _ini.WriteValue(nameof(TabWindowHeight).ToLower(), _sectionName, TabWindowHeight.ToString());
            _ini.WriteValue(nameof(MainWindowTabSelected).ToLower(), _sectionName, MainWindowTabSelected);
            _ini.Save();
        }

        public override void Load()
        {
            MainWindowWidth = _ini.GetValue(nameof(MainWindowWidth).ToLower(), _sectionName, MainWindowWidth);
            MainWindowHeight = _ini.GetValue(nameof(MainWindowHeight).ToLower(), _sectionName, MainWindowHeight);
            TabWindowWidth = _ini.GetValue(nameof(TabWindowWidth).ToLower(), _sectionName, TabWindowWidth);
            TabWindowHeight = _ini.GetValue(nameof(TabWindowHeight).ToLower(), _sectionName, TabWindowHeight);
            MainWindowTabSelected = _ini.GetValue(nameof(MainWindowTabSelected).ToLower(), _sectionName, MainWindowTabSelected);
        }

        public override void Update(SystemConfigBase newConfig)
        {
            UpdateBase(this, newConfig, typeof(SystemConfigLocality));
        }

        #endregion
    }
}
