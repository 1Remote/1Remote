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


        private string _mainWindowTabSelected = "";
        public string MainWindowTabSelected
        {
            get => _mainWindowTabSelected;
            set => SetAndNotifyIfChanged(nameof(MainWindowTabSelected), ref _mainWindowTabSelected, value);
        }

        private double _mainWindowWidth = 680;
        public double MainWindowWidth
        {
            get => _mainWindowWidth;
            set => SetAndNotifyIfChanged(nameof(MainWindowWidth), ref _mainWindowWidth, value);
        }


        private double _mainWindowHeight = 530;
        public double MainWindowHeight
        {
            get => _mainWindowHeight;
            set => SetAndNotifyIfChanged(nameof(MainWindowHeight), ref _mainWindowHeight, value);
        }




        private double _tabWindowWidth = 800;
        public double TabWindowWidth
        {
            get => _tabWindowWidth;
            set => SetAndNotifyIfChanged(nameof(TabWindowWidth), ref _tabWindowWidth, value);
        }


        private double _tabWindowHeight = 600;
        public double TabWindowHeight
        {
            get => _tabWindowHeight;
            set => SetAndNotifyIfChanged(nameof(TabWindowHeight), ref _tabWindowHeight, value);
        }


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
            _mainWindowWidth = _ini.GetValue(nameof(MainWindowWidth).ToLower(), _sectionName, MainWindowWidth);
            _mainWindowHeight = _ini.GetValue(nameof(MainWindowHeight).ToLower(), _sectionName, MainWindowHeight);
            _tabWindowWidth = _ini.GetValue(nameof(TabWindowWidth).ToLower(), _sectionName, TabWindowWidth);
            _tabWindowHeight = _ini.GetValue(nameof(TabWindowHeight).ToLower(), _sectionName, TabWindowHeight);
            MainWindowTabSelected = _ini.GetValue(nameof(MainWindowTabSelected).ToLower(), _sectionName, MainWindowTabSelected);
        }

        public override void Update(SystemConfigBase newConfig)
        {
            UpdateBase(this, newConfig, typeof(SystemConfigLocality));
        }

        #endregion
    }
}
