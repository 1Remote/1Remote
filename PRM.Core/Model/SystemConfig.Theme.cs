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
                _mainColor1 = theme.PrimaryMidColor;
                _mainColor1Lighter = theme.PrimaryLightColor;
                _mainColor1Darker = theme.PrimaryDarkColor;
                _mainColor1Foreground = theme.PrimaryTextColor;
                _mainColor2 = theme.AccentMidColor;
                _mainColor2Lighter = theme.AccentLightColor;
                _mainColor2Darker = theme.AccentDarkColor;
                _mainColor2Foreground = theme.AccentTextColor;
                _mainBgColor = theme.BackgroundColor;
                _mainBgColorForeground = theme.BackgroundTextColor;
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

        public string PrimaryMidColor
        {
            get => _mainColor1;
            set
            {
                try
                {
                    var color = ColorAndBrushHelper.HexColorToMediaColor(value);
                    PrimaryLightColor = System.Drawing.ColorTranslator.ToHtml(System.Drawing.Color.FromArgb(Math.Min(color.R + 50, 255), Math.Min(color.G + 45, 255), Math.Min(color.B + 40, 255)));
                    PrimaryDarkColor = System.Drawing.ColorTranslator.ToHtml(System.Drawing.Color.FromArgb((int)(color.R * 0.8), (int)(color.G * 0.8), (int)(color.B * 0.8)));
                    SetAndNotifyIfChanged(nameof(PrimaryMidColor), ref _mainColor1, value);
                    RaisePropertyChanged(nameof(PrimaryLightColor));
                    RaisePropertyChanged(nameof(PrimaryDarkColor));
                }
                catch (Exception e)
                {
                    SimpleLogHelper.Debug(e);
                }
            }
        }

        private string _mainColor1Lighter = "#445a68";

        public string PrimaryLightColor
        {
            get => _mainColor1Lighter;
            set => SetAndNotifyIfChanged(nameof(PrimaryLightColor), ref _mainColor1Lighter, value);
        }

        private string _mainColor1Darker = "#0c2230";

        public string PrimaryDarkColor
        {
            get => _mainColor1Darker;
            set => SetAndNotifyIfChanged(nameof(PrimaryDarkColor), ref _mainColor1Darker, value);
        }

        private string _mainColor1Foreground = "#ffffff";

        public string PrimaryTextColor
        {
            get => _mainColor1Foreground;
            set => SetAndNotifyIfChanged(nameof(PrimaryTextColor), ref _mainColor1Foreground, value);
        }

        private string _mainColor2 = "#e83d61";

        public string AccentMidColor
        {
            get => _mainColor2;
            set
            {
                try
                {
                    var color = ColorAndBrushHelper.HexColorToMediaColor(value);
                    AccentLightColor = System.Drawing.ColorTranslator.ToHtml(System.Drawing.Color.FromArgb(Math.Min(color.R + 50, 255), Math.Min(color.G + 45, 255), Math.Min(color.B + 40, 255)));
                    AccentDarkColor = System.Drawing.ColorTranslator.ToHtml(System.Drawing.Color.FromArgb((int)(color.R * 0.8), (int)(color.G * 0.8), (int)(color.B * 0.8)));
                    SetAndNotifyIfChanged(nameof(AccentMidColor), ref _mainColor2, value);
                    RaisePropertyChanged(nameof(AccentLightColor));
                    RaisePropertyChanged(nameof(AccentDarkColor));
                }
                catch (Exception e)
                {
                    SimpleLogHelper.Error(e);
                }
            }
        }

        private string _mainColor2Lighter = "#ed6884";

        public string AccentLightColor
        {
            get => _mainColor2Lighter;
            set => SetAndNotifyIfChanged(nameof(AccentLightColor), ref _mainColor2Lighter, value);
        }

        private string _mainColor2Darker = "#b5304c";

        public string AccentDarkColor
        {
            get => _mainColor2Darker;
            set => SetAndNotifyIfChanged(nameof(AccentDarkColor), ref _mainColor2Darker, value);
        }

        private string _mainColor2Foreground = "#ffffff";

        public string AccentTextColor
        {
            get => _mainColor2Foreground;
            set => SetAndNotifyIfChanged(nameof(AccentTextColor), ref _mainColor2Foreground, value);
        }

        private string _mainBgColor = "#ced8e1";

        public string BackgroundColor
        {
            get => _mainBgColor;
            set => SetAndNotifyIfChanged(nameof(BackgroundColor), ref _mainBgColor, value);
        }

        private string _mainBgColorForeground = "#000000";

        public string BackgroundTextColor
        {
            get => _mainBgColorForeground;
            set => SetAndNotifyIfChanged(nameof(BackgroundTextColor), ref _mainBgColorForeground, value);
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
            _ini.WriteValue(nameof(PrimaryMidColor).ToLower(), _sectionName, PrimaryMidColor);
            _ini.WriteValue(nameof(PrimaryLightColor).ToLower(), _sectionName, PrimaryLightColor);
            _ini.WriteValue(nameof(PrimaryDarkColor).ToLower(), _sectionName, PrimaryDarkColor);
            _ini.WriteValue(nameof(PrimaryTextColor).ToLower(), _sectionName, PrimaryTextColor);
            _ini.WriteValue(nameof(AccentMidColor).ToLower(), _sectionName, AccentMidColor);
            _ini.WriteValue(nameof(AccentLightColor).ToLower(), _sectionName, AccentLightColor);
            _ini.WriteValue(nameof(AccentDarkColor).ToLower(), _sectionName, AccentDarkColor);
            _ini.WriteValue(nameof(AccentTextColor).ToLower(), _sectionName, AccentTextColor);
            _ini.WriteValue(nameof(BackgroundColor).ToLower(), _sectionName, BackgroundColor);
            _ini.WriteValue(nameof(BackgroundTextColor).ToLower(), _sectionName, BackgroundTextColor);
            _ini.WriteValue(nameof(PuttyFontSize).ToLower(), _sectionName, PuttyFontSize.ToString());
            _ini.WriteValue(nameof(PuttyThemeName).ToLower(), _sectionName, PuttyThemeName);
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

            _mainColor1 = _ini.GetValue(nameof(PrimaryMidColor).ToLower(), _sectionName, PrimaryMidColor);
            _mainColor1Lighter = _ini.GetValue(nameof(PrimaryLightColor).ToLower(), _sectionName, PrimaryLightColor);
            _mainColor1Darker = _ini.GetValue(nameof(PrimaryDarkColor).ToLower(), _sectionName, PrimaryDarkColor);
            _mainColor1Foreground = _ini.GetValue(nameof(PrimaryTextColor).ToLower(), _sectionName, PrimaryTextColor);
            _mainColor2 = _ini.GetValue(nameof(AccentMidColor).ToLower(), _sectionName, AccentMidColor);
            _mainColor2Lighter = _ini.GetValue(nameof(AccentLightColor).ToLower(), _sectionName, AccentLightColor);
            _mainColor2Darker = _ini.GetValue(nameof(AccentDarkColor).ToLower(), _sectionName, AccentDarkColor);
            _mainColor2Foreground = _ini.GetValue(nameof(AccentTextColor).ToLower(), _sectionName, AccentTextColor);
            _mainBgColor = _ini.GetValue(nameof(BackgroundColor).ToLower(), _sectionName, BackgroundColor);
            _mainBgColorForeground = _ini.GetValue(nameof(BackgroundTextColor).ToLower(), _sectionName, BackgroundTextColor);
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
                SetKey(rd, "PrimaryMidColor", ColorAndBrushHelper.HexColorToMediaColor(PrimaryMidColor));
                SetKey(rd, "PrimaryLightColor", ColorAndBrushHelper.HexColorToMediaColor(PrimaryLightColor));
                SetKey(rd, "PrimaryDarkColor", ColorAndBrushHelper.HexColorToMediaColor(PrimaryDarkColor));
                SetKey(rd, "PrimaryTextColor", ColorAndBrushHelper.HexColorToMediaColor(PrimaryTextColor));
                SetKey(rd, "AccentMidColor", ColorAndBrushHelper.HexColorToMediaColor(AccentMidColor));
                SetKey(rd, "AccentLightColor", ColorAndBrushHelper.HexColorToMediaColor(AccentLightColor));
                SetKey(rd, "AccentDarkColor", ColorAndBrushHelper.HexColorToMediaColor(AccentDarkColor));
                SetKey(rd, "AccentTextColor", ColorAndBrushHelper.HexColorToMediaColor(AccentTextColor));
                SetKey(rd, "BackgroundColor", ColorAndBrushHelper.HexColorToMediaColor(BackgroundColor));
                SetKey(rd, "BackgroundTextColor", ColorAndBrushHelper.HexColorToMediaColor(BackgroundTextColor));


                SetKey(rd, "PrimaryMidBrush", ColorAndBrushHelper.ColorToMediaBrush(PrimaryMidColor));
                SetKey(rd, "PrimaryLightBrush", ColorAndBrushHelper.ColorToMediaBrush(PrimaryLightColor));
                SetKey(rd, "PrimaryDarkBrush", ColorAndBrushHelper.ColorToMediaBrush(PrimaryDarkColor));
                SetKey(rd, "PrimaryTextBrush", ColorAndBrushHelper.ColorToMediaBrush(PrimaryTextColor));
                SetKey(rd, "AccentMidBrush", ColorAndBrushHelper.ColorToMediaBrush(AccentMidColor));
                SetKey(rd, "AccentLightBrush", ColorAndBrushHelper.ColorToMediaBrush(AccentLightColor));
                SetKey(rd, "AccentDarkBrush", ColorAndBrushHelper.ColorToMediaBrush(AccentLightColor));
                SetKey(rd, "AccentTextBrush", ColorAndBrushHelper.ColorToMediaBrush(AccentTextColor));
                SetKey(rd, "BackgroundBrush", ColorAndBrushHelper.ColorToMediaBrush(BackgroundColor));
                SetKey(rd, "BackgroundTextBrush", ColorAndBrushHelper.ColorToMediaBrush(BackgroundTextColor));

                SetKey(rd, "PrimaryColor", ColorAndBrushHelper.HexColorToMediaColor(AccentMidColor));
                SetKey(rd, "DarkPrimaryColor", ColorAndBrushHelper.HexColorToMediaColor(AccentDarkColor));
                SetKey(rd, "PrimaryDarkColor", ColorAndBrushHelper.HexColorToMediaColor(AccentTextColor));

                foreach (var r in rs)
                {
                    _appResourceDictionary.MergedDictionaries.Remove(r);
                }
                _appResourceDictionary.MergedDictionaries.Add(rd);

                RaisePropertyChanged(nameof(PrimaryMidColor));
                RaisePropertyChanged(nameof(PrimaryLightColor));
                RaisePropertyChanged(nameof(PrimaryDarkColor));
                RaisePropertyChanged(nameof(PrimaryTextColor));
                RaisePropertyChanged(nameof(AccentMidColor));
                RaisePropertyChanged(nameof(AccentLightColor));
                RaisePropertyChanged(nameof(AccentDarkColor));
                RaisePropertyChanged(nameof(AccentTextColor));
                RaisePropertyChanged(nameof(BackgroundColor));
                RaisePropertyChanged(nameof(BackgroundTextColor));
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
                        var theme = PrmColorThemes[PrmColorThemeName];
                        _mainColor1 = theme.PrimaryMidColor;
                        _mainColor1Lighter = theme.PrimaryLightColor;
                        _mainColor1Darker = theme.PrimaryDarkColor;
                        _mainColor1Foreground = theme.PrimaryTextColor;
                        _mainColor2 = theme.AccentMidColor;
                        _mainColor2Lighter = theme.AccentLightColor;
                        _mainColor2Darker = theme.AccentDarkColor;
                        _mainColor2Foreground = theme.AccentTextColor;
                        _mainBgColor = theme.BackgroundColor;
                        _mainBgColorForeground = theme.BackgroundTextColor;
                        Save();
                        RaisePropertyChanged(nameof(PrmColorThemeName));
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