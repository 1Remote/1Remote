using System;
using System.IO;
using System.Windows;
using Shawn.Utils;

namespace PRM.Service
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

    internal class LocalitySettings
    {
        public double MainWindowWidth;
        public double MainWindowHeight;
        public double TabWindowWidth;
        public double TabWindowHeight;
        public WindowState TabWindowState;
        public EnumServerOrderBy ServerOrderBy
    }

    public sealed class LocalityService : NotifyPropertyChangedBase
    {
        private readonly Ini _ini;
        public LocalityService()
        {
            _ini = new Ini(Path.Combine(AppPathHelper.Instance.LocalityDirPath, "locality.ini"));
            Load();
        }

        public double MainWindowWidth
        {
            get => _mainWindowWidth;
            set
            {
                _mainWindowWidth = value;
                Save();
            }
        }

        public double MainWindowHeight
        {
            get => _mainWindowHeight;
            set
            {
                _mainWindowHeight = value;
                Save();
            }
        }

        public double TabWindowWidth
        {
            get => _tabWindowWidth;
            set
            {
                _tabWindowWidth = value;
                Save();
            }
        }

        public double TabWindowHeight
        {
            get => _tabWindowHeight;
            set
            {
                _tabWindowHeight = value;
                Save();
            }
        }

        public WindowState TabWindowState
        {
            get => _tabWindowState;
            set
            {
                _tabWindowState = value;
                Save();
            }
        }
        private EnumServerOrderBy _serverOrderBy = EnumServerOrderBy.NameAsc;
        public EnumServerOrderBy ServerOrderBy
        {
            get => _serverOrderBy;
            set
            {
                SetAndNotifyIfChanged(ref _serverOrderBy, value);
                Save();
            }
        }


        private double _mainWindowWidth = 800;
        private double _mainWindowHeight = 530;
        private double _tabWindowWidth = 800;
        private double _tabWindowHeight = 680;
        private WindowState _tabWindowState = WindowState.Normal;

        #region Interface

        private const string SectionName = "Locality";
        private bool _saveEnabled = true;
        private void Save()
        {
            if (_saveEnabled)
            {
                _ini.WriteValue(nameof(MainWindowWidth).ToLower(), SectionName, MainWindowWidth.ToString());
                _ini.WriteValue(nameof(MainWindowHeight).ToLower(), SectionName, MainWindowHeight.ToString());
                _ini.WriteValue(nameof(TabWindowWidth).ToLower(), SectionName, TabWindowWidth.ToString());
                _ini.WriteValue(nameof(TabWindowHeight).ToLower(), SectionName, TabWindowHeight.ToString());
                _ini.WriteValue(nameof(ServerOrderBy).ToLower(), SectionName, ServerOrderBy.ToString());
                _ini.Save();
            }
        }

        private void Load()
        {
            _saveEnabled = false;
            MainWindowWidth = _ini.GetValue(nameof(MainWindowWidth).ToLower(), SectionName, MainWindowWidth);
            MainWindowHeight = _ini.GetValue(nameof(MainWindowHeight).ToLower(), SectionName, MainWindowHeight);
            TabWindowWidth = _ini.GetValue(nameof(TabWindowWidth).ToLower(), SectionName, TabWindowWidth);
            TabWindowHeight = _ini.GetValue(nameof(TabWindowHeight).ToLower(), SectionName, TabWindowHeight);
            if (Enum.TryParse<EnumServerOrderBy>(_ini.GetValue(nameof(ServerOrderBy).ToLower(), SectionName, ServerOrderBy.ToString()), out var so))
                ServerOrderBy = so;
            _saveEnabled = true;
        }

        #endregion Interface
    }
}