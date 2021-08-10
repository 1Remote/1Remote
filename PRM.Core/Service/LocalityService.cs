using System.Windows;
using Shawn.Utils;

namespace PRM.Core.Service
{
    public sealed class LocalityService
    {
        private readonly Ini _ini;
        public LocalityService(Ini ini)
        {
            _ini = ini;
            Load();
        }

        public string MainWindowTabSelected { get; set; } = "";
        public double MainWindowWidth { get; set; } = 800;
        public double MainWindowHeight { get; set; } = 530;
        public double TabWindowWidth { get; set; } = 800;
        public double TabWindowHeight { get; set; } = 680;
        public WindowState TabWindowState = WindowState.Normal;

        #region Interface

        private const string SectionName = "Locality";

        public void Save()
        {
            _ini.WriteValue(nameof(MainWindowWidth).ToLower(), SectionName, MainWindowWidth.ToString());
            _ini.WriteValue(nameof(MainWindowHeight).ToLower(), SectionName, MainWindowHeight.ToString());
            _ini.WriteValue(nameof(TabWindowWidth).ToLower(), SectionName, TabWindowWidth.ToString());
            _ini.WriteValue(nameof(TabWindowHeight).ToLower(), SectionName, TabWindowHeight.ToString());
            _ini.WriteValue(nameof(MainWindowTabSelected).ToLower(), SectionName, MainWindowTabSelected);
            _ini.Save();
        }

        public void Load()
        {
            MainWindowWidth = _ini.GetValue(nameof(MainWindowWidth).ToLower(), SectionName, MainWindowWidth);
            MainWindowHeight = _ini.GetValue(nameof(MainWindowHeight).ToLower(), SectionName, MainWindowHeight);
            TabWindowWidth = _ini.GetValue(nameof(TabWindowWidth).ToLower(), SectionName, TabWindowWidth);
            TabWindowHeight = _ini.GetValue(nameof(TabWindowHeight).ToLower(), SectionName, TabWindowHeight);
            MainWindowTabSelected = _ini.GetValue(nameof(MainWindowTabSelected).ToLower(), SectionName, MainWindowTabSelected);
        }

        #endregion Interface
    }
}