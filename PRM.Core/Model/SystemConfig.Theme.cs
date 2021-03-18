using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using PRM.Core.Protocol;
using PRM.Core.Protocol.Putty;
using PRM.Core.Resources.Theme;
using Shawn.Utils;

namespace PRM.Core.Model
{
    public enum EnumTabUI
    {
        ChromeLike,
        Classical,
    }

    public enum EnumServerListPageUI
    {
        Card,
        List,
    }

    public sealed class SystemConfigTheme : SystemConfigBase
    {
        private readonly ResourceDictionary _appResourceDictionary = null;

        public SystemConfigTheme(ResourceDictionary appResourceDictionary, Ini ini) : base(ini)
        {
            Debug.Assert(appResourceDictionary != null);
            _appResourceDictionary = appResourceDictionary;
            Load();
        }

        private int _puttyFontSize = 14;

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

        private ObservableCollection<string> _puttyThemeNames = new ObservableCollection<string>();

        public ObservableCollection<string> PuttyThemeNames
        {
            get => _puttyThemeNames;
            set => SetAndNotifyIfChanged(nameof(PuttyThemeNames), ref _puttyThemeNames, value);
        }

        private string _prmColorThemeName = "";

        public string PrmColorThemeName
        {
            get => _prmColorThemeName;
            set
            {
                if (!PrmColorThemes.ContainsKey(value)) return;
                var theme = PrmColorThemes[value];
                _mainColor1 = theme.MainColor1;
                _mainColor1Lighter = theme.MainColor1Lighter;
                _mainColor1Darker = theme.MainColor1Darker;
                _mainColor1Foreground = theme.MainColor1Foreground;
                _mainColor2 = theme.MainColor2;
                _mainColor2Lighter = theme.MainColor2Lighter;
                _mainColor2Darker = theme.MainColor2Darker;
                _mainColor2Foreground = theme.MainColor2Foreground;
                _mainBgColor = theme.MainBgColor;
                _mainBgColorForeground = theme.MainBgColorForeground;
                Save();
                SetAndNotifyIfChanged(nameof(PrmColorThemeName), ref _prmColorThemeName, value);
            }
        }

        private ObservableCollection<string> _prmColorThemeNames = new ObservableCollection<string>();

        public ObservableCollection<string> PrmColorThemeNames
        {
            get => _prmColorThemeNames;
            set => SetAndNotifyIfChanged(nameof(PrmColorThemeNames), ref _prmColorThemeNames, value);
        }

        private Dictionary<string, PrmColorTheme> _prmColorThemes;

        public Dictionary<string, PrmColorTheme> PrmColorThemes
        {
            get => _prmColorThemes;
            set => SetAndNotifyIfChanged(nameof(PrmColorThemes), ref _prmColorThemes, value);
        }

        private string _mainColor1 = "#102b3e";

        public string MainColor1
        {
            get => _mainColor1;
            set
            {
                try
                {
                    var color = ColorAndBrushHelper.HexColorToMediaColor(value);
                    MainColor1Lighter = System.Drawing.ColorTranslator.ToHtml(System.Drawing.Color.FromArgb(Math.Min(color.R + 50, 255), Math.Min(color.G + 45, 255), Math.Min(color.B + 40, 255)));
                    MainColor1Darker = System.Drawing.ColorTranslator.ToHtml(System.Drawing.Color.FromArgb((int)(color.R * 0.8), (int)(color.G * 0.8), (int)(color.B * 0.8)));
                    SetAndNotifyIfChanged(nameof(MainColor1), ref _mainColor1, value);
                    RaisePropertyChanged(nameof(MainColor1Lighter));
                    RaisePropertyChanged(nameof(MainColor1Darker));
                }
                catch (Exception e)
                {
                    SimpleLogHelper.Debug(e);
                }
            }
        }

        private string _mainColor1Lighter = "#445a68";

        public string MainColor1Lighter
        {
            get => _mainColor1Lighter;
            set => SetAndNotifyIfChanged(nameof(MainColor1Lighter), ref _mainColor1Lighter, value);
        }

        private string _mainColor1Darker = "#0c2230";

        public string MainColor1Darker
        {
            get => _mainColor1Darker;
            set => SetAndNotifyIfChanged(nameof(MainColor1Darker), ref _mainColor1Darker, value);
        }

        private string _mainColor1Foreground = "#ffffff";

        public string MainColor1Foreground
        {
            get => _mainColor1Foreground;
            set => SetAndNotifyIfChanged(nameof(MainColor1Foreground), ref _mainColor1Foreground, value);
        }

        private string _mainColor2 = "#e83d61";

        public string MainColor2
        {
            get => _mainColor2;
            set
            {
                try
                {
                    var color = ColorAndBrushHelper.HexColorToMediaColor(value);
                    MainColor2Lighter = System.Drawing.ColorTranslator.ToHtml(System.Drawing.Color.FromArgb(Math.Min(color.R + 50, 255), Math.Min(color.G + 45, 255), Math.Min(color.B + 40, 255)));
                    MainColor2Darker = System.Drawing.ColorTranslator.ToHtml(System.Drawing.Color.FromArgb((int)(color.R * 0.8), (int)(color.G * 0.8), (int)(color.B * 0.8)));
                    SetAndNotifyIfChanged(nameof(MainColor2), ref _mainColor2, value);
                    RaisePropertyChanged(nameof(MainColor2Lighter));
                    RaisePropertyChanged(nameof(MainColor2Darker));
                }
                catch (Exception e)
                {
                    SimpleLogHelper.Error(e);
                }
            }
        }

        private string _mainColor2Lighter = "#ed6884";

        public string MainColor2Lighter
        {
            get => _mainColor2Lighter;
            set => SetAndNotifyIfChanged(nameof(MainColor2Lighter), ref _mainColor2Lighter, value);
        }

        private string _mainColor2Darker = "#b5304c";

        public string MainColor2Darker
        {
            get => _mainColor2Darker;
            set => SetAndNotifyIfChanged(nameof(MainColor2Darker), ref _mainColor2Darker, value);
        }

        private string _mainColor2Foreground = "#ffffff";

        public string MainColor2Foreground
        {
            get => _mainColor2Foreground;
            set => SetAndNotifyIfChanged(nameof(MainColor2Foreground), ref _mainColor2Foreground, value);
        }

        private string _mainBgColor = "#ced8e1";

        public string MainBgColor
        {
            get => _mainBgColor;
            set => SetAndNotifyIfChanged(nameof(MainBgColor), ref _mainBgColor, value);
        }

        private string _mainBgColorForeground = "#000000";

        public string MainBgColorForeground
        {
            get => _mainBgColorForeground;
            set => SetAndNotifyIfChanged(nameof(MainBgColorForeground), ref _mainBgColorForeground, value);
        }

        private EnumTabUI _tabUi = EnumTabUI.Classical;

        public EnumTabUI TabUI
        {
            get => _tabUi;
            set => SetAndNotifyIfChanged(nameof(TabUI), ref _tabUi, value);
        }

        private EnumServerListPageUI _serverListPageUi = EnumServerListPageUI.Card;

        public EnumServerListPageUI ServerListPageUI
        {
            get => _serverListPageUi;
            set => SetAndNotifyIfChanged(nameof(ServerListPageUI), ref _serverListPageUi, value);
        }

        #region Interface

        private const string _sectionName = "Theme";

        public override void Save()
        {
            _ini.WriteValue(nameof(PrmColorThemeName).ToLower(), _sectionName, PrmColorThemeName);
            _ini.WriteValue(nameof(MainColor1).ToLower(), _sectionName, MainColor1);
            _ini.WriteValue(nameof(MainColor1Lighter).ToLower(), _sectionName, MainColor1Lighter);
            _ini.WriteValue(nameof(MainColor1Darker).ToLower(), _sectionName, MainColor1Darker);
            _ini.WriteValue(nameof(MainColor1Foreground).ToLower(), _sectionName, MainColor1Foreground);
            _ini.WriteValue(nameof(MainColor2).ToLower(), _sectionName, MainColor2);
            _ini.WriteValue(nameof(MainColor2Lighter).ToLower(), _sectionName, MainColor2Lighter);
            _ini.WriteValue(nameof(MainColor2Darker).ToLower(), _sectionName, MainColor2Darker);
            _ini.WriteValue(nameof(MainColor2Foreground).ToLower(), _sectionName, MainColor2Foreground);
            _ini.WriteValue(nameof(MainBgColor).ToLower(), _sectionName, MainBgColor);
            _ini.WriteValue(nameof(MainBgColorForeground).ToLower(), _sectionName, MainBgColorForeground);
            _ini.WriteValue(nameof(PuttyFontSize).ToLower(), _sectionName, PuttyFontSize.ToString());
            _ini.WriteValue(nameof(PuttyThemeName).ToLower(), _sectionName, PuttyThemeName);
            _ini.WriteValue(nameof(TabUI).ToLower(), _sectionName, TabUI.ToString());
            _ini.WriteValue(nameof(ServerListPageUI).ToLower(), _sectionName, ServerListPageUI.ToString());
            _ini.Save();
            ApplyPrmColorTheme();
        }

        private void LoadThemeFromIni()
        {
            PrmColorThemes = PRM.Core.Resources.Theme.PrmColorThemes.GetThemes();
            PrmColorThemeNames = new ObservableCollection<string>(PrmColorThemes.Keys);

            _prmColorThemeName = _ini.GetValue(nameof(PrmColorThemeName).ToLower(), _sectionName, _prmColorThemeName);
            if (!PrmColorThemeNames.Contains(_prmColorThemeName))
                _prmColorThemeName = PrmColorThemeNames.First();

            _mainColor1 = _ini.GetValue(nameof(MainColor1).ToLower(), _sectionName, MainColor1);
            _mainColor1Lighter = _ini.GetValue(nameof(MainColor1Lighter).ToLower(), _sectionName, MainColor1Lighter);
            _mainColor1Darker = _ini.GetValue(nameof(MainColor1Darker).ToLower(), _sectionName, MainColor1Darker);
            _mainColor1Foreground = _ini.GetValue(nameof(MainColor1Foreground).ToLower(), _sectionName, MainColor1Foreground);
            _mainColor2 = _ini.GetValue(nameof(MainColor2).ToLower(), _sectionName, MainColor2);
            _mainColor2Lighter = _ini.GetValue(nameof(MainColor2Lighter).ToLower(), _sectionName, MainColor2Lighter);
            _mainColor2Darker = _ini.GetValue(nameof(MainColor2Darker).ToLower(), _sectionName, MainColor2Darker);
            _mainColor2Foreground = _ini.GetValue(nameof(MainColor2Foreground).ToLower(), _sectionName, MainColor2Foreground);
            _mainBgColor = _ini.GetValue(nameof(MainBgColor).ToLower(), _sectionName, MainBgColor);
            _mainBgColorForeground = _ini.GetValue(nameof(MainBgColorForeground).ToLower(), _sectionName, MainBgColorForeground);
        }

        private void LoadPuttyThemeFromIni()
        {
            ReloadPuttyThemes();
            PuttyThemeName = _ini.GetValue(nameof(PuttyThemeName).ToLower(), _sectionName, PuttyThemeName);
            if (!PuttyThemeNames.Contains(PuttyThemeName))
                PuttyThemeName = PuttyThemeNames.First();
            PuttyFontSize = _ini.GetValue(nameof(PuttyFontSize).ToLower(), _sectionName, PuttyFontSize);
        }

        public override void Load()
        {
            if (!_ini.ContainsKey(nameof(PrmColorThemeName).ToLower(), _sectionName))
                return;

            StopAutoSave = true;

            LoadThemeFromIni();

            LoadPuttyThemeFromIni();

            if (Enum.TryParse<EnumTabUI>(_ini.GetValue(nameof(TabUI).ToLower(), _sectionName, TabUI.ToString()), out var tu))
                TabUI = tu;

            if (Enum.TryParse<EnumServerListPageUI>(_ini.GetValue(nameof(ServerListPageUI).ToLower(), _sectionName, ServerListPageUI.ToString()), out var slu))
                ServerListPageUI = slu;

            StopAutoSave = false;
            ApplyPrmColorTheme();
        }

        public override void Update(SystemConfigBase newConfig)
        {
            UpdateBase(this, newConfig, typeof(SystemConfigTheme));
        }

        private Dictionary<string, List<PuttyOptionItem>> _puttyThemes = new Dictionary<string, List<PuttyOptionItem>>();

        public List<PuttyOptionItem> SelectedPuttyTheme
        {
            get
            {
                if (_puttyThemes.ContainsKey(PuttyThemeName))
                    return _puttyThemes[PuttyThemeName];
                return null;
            }
        }

        public void ReloadPuttyThemes()
        {
            _puttyThemes = PuttyColorThemes.GetThemes();
            var puttyThemeNames = new ObservableCollection<string>(_puttyThemes.Keys);
            _puttyThemeNames = puttyThemeNames;
        }

        #endregion Interface

        private void ApplyPrmColorTheme()
        {
            Debug.Assert(_appResourceDictionary != null);
            const string resourceTypeKey = "__Resource_Type_Key";
            const string resourceTypeValue = "__Resource_Type_Value=colortheme";
            void SetKey(IDictionary rd, string key, object value)
            {
                if (!rd.Contains(key))
                    rd.Add(key, value);
                else
                    rd[key] = value;
            }
            var rs = _appResourceDictionary.MergedDictionaries.Where(o =>
                (o.Source != null && o.Source.IsAbsoluteUri && o.Source.AbsolutePath.ToLower().IndexOf("Theme/Default.xaml".ToLower()) >= 0)
                || o[resourceTypeKey]?.ToString() == resourceTypeValue).ToArray();
            try
            {
                // create new theme resources
                var rd = new ResourceDictionary();
                SetKey(rd, resourceTypeKey, resourceTypeValue);
                SetKey(rd, "MainColor1", ColorAndBrushHelper.HexColorToMediaColor(MainColor1));
                SetKey(rd, "MainColor1Lighter", ColorAndBrushHelper.HexColorToMediaColor(MainColor1Lighter));
                SetKey(rd, "MainColor1Darker", ColorAndBrushHelper.HexColorToMediaColor(MainColor1Darker));
                SetKey(rd, "MainColor1Foreground", ColorAndBrushHelper.HexColorToMediaColor(MainColor1Foreground));
                SetKey(rd, "MainColor2", ColorAndBrushHelper.HexColorToMediaColor(MainColor2));
                SetKey(rd, "MainColor2Lighter", ColorAndBrushHelper.HexColorToMediaColor(MainColor2Lighter));
                SetKey(rd, "MainColor2Darker", ColorAndBrushHelper.HexColorToMediaColor(MainColor2Darker));
                SetKey(rd, "MainColor2Foreground", ColorAndBrushHelper.HexColorToMediaColor(MainColor2Foreground));
                SetKey(rd, "MainBgColor", ColorAndBrushHelper.HexColorToMediaColor(MainBgColor));
                SetKey(rd, "MainBgColorForeground", ColorAndBrushHelper.HexColorToMediaColor(MainBgColorForeground));

                SetKey(rd, "MainColor1Brush", ColorAndBrushHelper.ColorToMediaBrush(MainColor1));
                SetKey(rd, "MainColor1LighterBrush", ColorAndBrushHelper.ColorToMediaBrush(MainColor1Lighter));
                SetKey(rd, "MainColor1DarkerBrush", ColorAndBrushHelper.ColorToMediaBrush(MainColor1Darker));
                SetKey(rd, "MainColor1ForegroundBrush", ColorAndBrushHelper.ColorToMediaBrush(MainColor1Foreground));
                SetKey(rd, "MainColor2Brush", ColorAndBrushHelper.ColorToMediaBrush(MainColor2));
                SetKey(rd, "MainColor2LighterBrush", ColorAndBrushHelper.ColorToMediaBrush(MainColor2Lighter));
                SetKey(rd, "MainColor2DarkerBrush", ColorAndBrushHelper.ColorToMediaBrush(MainColor2Lighter));
                SetKey(rd, "MainColor2ForegroundBrush", ColorAndBrushHelper.ColorToMediaBrush(MainColor2Foreground));
                SetKey(rd, "MainBgColorBrush", ColorAndBrushHelper.ColorToMediaBrush(MainBgColor));
                SetKey(rd, "MainBgColorForegroundBrush", ColorAndBrushHelper.ColorToMediaBrush(MainBgColorForeground));

                foreach (var r in rs)
                {
                    _appResourceDictionary.MergedDictionaries.Remove(r);
                }
                _appResourceDictionary.MergedDictionaries.Add(rd);

                RaisePropertyChanged(nameof(MainColor1));
                RaisePropertyChanged(nameof(MainColor1Lighter));
                RaisePropertyChanged(nameof(MainColor1Darker));
                RaisePropertyChanged(nameof(MainColor1Foreground));
                RaisePropertyChanged(nameof(MainColor2));
                RaisePropertyChanged(nameof(MainColor2Lighter));
                RaisePropertyChanged(nameof(MainColor2Darker));
                RaisePropertyChanged(nameof(MainColor2Foreground));
                RaisePropertyChanged(nameof(MainBgColor));
                RaisePropertyChanged(nameof(MainBgColorForeground));
            }
            catch (Exception e)
            {
                SimpleLogHelper.Warning(e);
            }
        }

        #region CMD

        private RelayCommand _cmdPrmThemeReset;

        public RelayCommand CmdPrmThemeReset
        {
            get
            {
                if (_cmdPrmThemeReset == null)
                {
                    _cmdPrmThemeReset = new RelayCommand((o) =>
                    {
                        PrmColorThemeName = PrmColorThemeName;
                    });
                }
                return _cmdPrmThemeReset;
            }
        }

        private RelayCommand _cmdToggleServerListPageUi;

        public RelayCommand CmdToggleServerListPageUI
        {
            get
            {
                if (_cmdToggleServerListPageUi == null)
                {
                    _cmdToggleServerListPageUi = new RelayCommand((o) =>
                    {
                        var array = (EnumServerListPageUI[])Enum.GetValues(typeof(EnumServerListPageUI));
                        for (var i = 0; i < array.Length; i++)
                        {
                            var e = array[i];
                            if (ServerListPageUI == e)
                            {
                                ServerListPageUI = i + 1 < array.Length ? array[i + 1] : array[0];
                                break;
                            }
                        }
                    });
                }
                return _cmdToggleServerListPageUi;
            }
        }

        #endregion CMD
    }
}