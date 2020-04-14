using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Shawn.Ulits;

namespace PRM.Core.Model
{
    public sealed class SystemConfigLocality : SystemConfigBase
    {
        //protected override void SetAndNotifyIfChanged<T>(string propertyName, ref T oldValue, T newValue)
        //{
        //    if (oldValue == null && newValue == null) return;
        //    if (oldValue != null && oldValue.Equals(newValue)) return;
        //    if (newValue != null && newValue.Equals(oldValue)) return;
        //    oldValue = newValue;
        //    RaisePropertyChanged(propertyName);
        //    Save();
        //}

        private new readonly Ini _ini;

        public SystemConfigLocality():base(null)
        {
            var appDateFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Process.GetCurrentProcess().MainModule.ModuleName.Replace(".", "_"));
            if (!Directory.Exists(appDateFolder))
                Directory.CreateDirectory(appDateFolder);
            var fileName = Path.Combine(appDateFolder, "locality.ini");
            _ini = new Ini(fileName);
            Load();
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


        #region Interface
        private const string _sectionName = "Locality";
        public override void Save()
        {
            _ini.WriteValue(nameof(MainWindowWidth).ToLower(), _sectionName, MainWindowWidth.ToString());
            _ini.WriteValue(nameof(MainWindowHeight).ToLower(), _sectionName, MainWindowHeight.ToString());
            _ini.Save();
        }

        public override void Load()
        {
            MainWindowWidth = _ini.GetValue(nameof(MainWindowWidth).ToLower(), _sectionName, MainWindowWidth);
            MainWindowHeight = _ini.GetValue(nameof(MainWindowHeight).ToLower(), _sectionName, MainWindowHeight);
        }

        public override void Update(SystemConfigBase newConfig)
        {
            UpdateBase(this, newConfig, typeof(SystemConfigLocality));
        }

        #endregion
    }
}
