using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PRM.Core.Protocol.Putty;
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


        private string _selectedPuttyThemeName = "";

        public string SelectedPuttyThemeName
        {
            get => _selectedPuttyThemeName;
            set => SetAndNotifyIfChanged(nameof(SelectedPuttyThemeName), ref _selectedPuttyThemeName, value);
        }

        private ObservableCollection<string> _puttyThemeNames= new ObservableCollection<string>();
        public ObservableCollection<string> PuttyThemeNames
        {
            get => _puttyThemeNames;
            set => SetAndNotifyIfChanged(nameof(PuttyThemeNames), ref _puttyThemeNames, value);
        }


        #region Interface
        private const string _sectionName = "Theme";
        public override void Save()
        {
            _ini.WriteValue(nameof(PuttyFontSize).ToLower(), _sectionName, PuttyFontSize.ToString());
            _ini.WriteValue(nameof(SelectedPuttyThemeName).ToLower(), _sectionName, SelectedPuttyThemeName.ToString());
            _ini.Save();
        }

        public override void Load()
        {
            StopAutoSave = true;
            PuttyFontSize = _ini.GetValue(nameof(PuttyFontSize).ToLower(), _sectionName, PuttyFontSize);
            SelectedPuttyThemeName = _ini.GetValue(nameof(SelectedPuttyThemeName).ToLower(), _sectionName, SelectedPuttyThemeName);
            ReloadThemes();
            if (string.IsNullOrEmpty(SelectedPuttyThemeName))
                SelectedPuttyThemeName = PuttyColorThemes.Get00__Default().Item1;
            StopAutoSave = false;
        }

        public override void Update(SystemConfigBase newConfig)
        {
            UpdateBase(this, newConfig, typeof(SystemConfigTheme));
        }

        private Dictionary<string, List<PuttyRegOptionItem>> _puttyThemes = new Dictionary<string, List<PuttyRegOptionItem>>();
        public List<PuttyRegOptionItem> SelectedPuttyTheme
        {
            get
            {
                if (_puttyThemes.ContainsKey(SelectedPuttyThemeName))
                    return _puttyThemes[SelectedPuttyThemeName];
                return null;
            }
        }


        public void ReloadThemes()
        {
            _puttyThemes = PuttyColorThemes.GetThemes();
            var puttyThemeNames = new ObservableCollection<string>(_puttyThemes.Keys);
            _puttyThemeNames = puttyThemeNames;
        }
        #endregion
    }
}