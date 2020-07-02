using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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


        private string _puttyThemeName = "";

        public string PuttyThemeName
        {
            get => _puttyThemeName;
            set => SetAndNotifyIfChanged(nameof(PuttyThemeName), ref _puttyThemeName, value);
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
            _ini.WriteValue(nameof(PuttyThemeName).ToLower(), _sectionName, PuttyThemeName.ToString());
            _ini.Save();
        }

        public override void Load()
        {
            StopAutoSave = true;
            PuttyFontSize = _ini.GetValue(nameof(PuttyFontSize).ToLower(), _sectionName, PuttyFontSize);
            PuttyThemeName = _ini.GetValue(nameof(PuttyThemeName).ToLower(), _sectionName, PuttyThemeName);
            ReloadThemes();
            if (string.IsNullOrEmpty(PuttyThemeName))
                PuttyThemeName = PuttyColorThemes.Get00__Default().Item1;
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
                if (_puttyThemes.ContainsKey(PuttyThemeName))
                    return _puttyThemes[PuttyThemeName];
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




        private RelayCommand _cmdPuttyThemeCustomize;
        public RelayCommand CmdPuttyThemeCustomize
        {
            get
            {
                if (_cmdPuttyThemeCustomize == null)
                {
                    _cmdPuttyThemeCustomize = new RelayCommand((o) =>
                    {
                        var puttyTheme = SelectedPuttyTheme;
                        if (!Directory.Exists(PuttyColorThemes.ThemeRegFileFolder))
                            Directory.CreateDirectory(PuttyColorThemes.ThemeRegFileFolder);
                        var fi = puttyTheme.ToRegFile(Path.Combine(PuttyColorThemes.ThemeRegFileFolder, PuttyThemeName + ".reg"));
                        if (fi != null)
                            System.Diagnostics.Process.Start("notepad.exe", fi.FullName);
                    });
                }
                return _cmdPuttyThemeCustomize;
            }
        }
    }
}