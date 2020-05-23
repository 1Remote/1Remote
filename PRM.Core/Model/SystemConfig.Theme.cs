using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Shawn.Ulits;

namespace PRM.Core.Model
{
    public sealed class SystemConfigTheme : SystemConfigBase
    {
        public SystemConfigTheme(Ini ini) : base(ini)
        {
            Load();
        }

        private int _puttyFontSize = 12;
        public int PuttyFontSize
        {
            get => _puttyFontSize;
            set => SetAndNotifyIfChanged(nameof(PuttyFontSize), ref _puttyFontSize, value);
        }


        private string _selectedPuttyTheme = "";

        public string SelectedPuttyTheme
        {
            get => _selectedPuttyTheme;
            set => SetAndNotifyIfChanged(nameof(SelectedPuttyTheme), ref _selectedPuttyTheme, value);
        }

        private ObservableCollection<string> _puttyThemes= new ObservableCollection<string>();
        public ObservableCollection<string> PuttyThemes
        {
            get => _puttyThemes;
            set => SetAndNotifyIfChanged(nameof(PuttyThemes), ref _puttyThemes, value);
        }


        #region Interface
        private const string _sectionName = "Theme";
        public override void Save()
        {
            _ini.WriteValue(nameof(PuttyFontSize).ToLower(), _sectionName, PuttyFontSize.ToString());
            _ini.WriteValue(nameof(SelectedPuttyTheme).ToLower(), _sectionName, SelectedPuttyTheme.ToString());
            _ini.Save();
        }

        public override void Load()
        {
            StopAutoSave = true;
            PuttyFontSize = _ini.GetValue(nameof(PuttyFontSize).ToLower(), _sectionName, PuttyFontSize);
            SelectedPuttyTheme = _ini.GetValue(nameof(SelectedPuttyTheme).ToLower(), _sectionName, SelectedPuttyTheme);
            StopAutoSave = false;
        }

        public override void Update(SystemConfigBase newConfig)
        {
            UpdateBase(this, newConfig, typeof(SystemConfigTheme));
        }

        #endregion
    }
}