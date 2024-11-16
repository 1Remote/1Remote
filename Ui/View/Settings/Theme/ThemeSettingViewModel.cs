using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using _1RM.Service;
using Shawn.Utils;
using Shawn.Utils.Wpf;

namespace _1RM.View.Settings.Theme
{
    public class ThemeSettingViewModel : NotifyPropertyChangedBase
    {
        private readonly ConfigurationService _configurationService;
        private readonly ThemeService _themeService;

        public ThemeSettingViewModel(ConfigurationService configurationService, ThemeService themeService)
        {
            _configurationService = configurationService;
            _themeService = themeService;
            LoadSystemFonts();
        }



        private void SetThemeByName(string name)
        {
            Debug.Assert(_themeService.Themes.ContainsKey(name));
            var theme = _themeService.Themes[name];
            _configurationService.Theme.PrimaryMidColor = theme.PrimaryMidColor;
            _configurationService.Theme.PrimaryLightColor = theme.PrimaryLightColor;
            _configurationService.Theme.PrimaryDarkColor = theme.PrimaryDarkColor;
            _configurationService.Theme.PrimaryTextColor = theme.PrimaryTextColor;
            _configurationService.Theme.AccentMidColor = theme.AccentMidColor;
            _configurationService.Theme.AccentLightColor = theme.AccentLightColor;
            _configurationService.Theme.AccentDarkColor = theme.AccentDarkColor;
            _configurationService.Theme.AccentTextColor = theme.AccentTextColor;
            _configurationService.Theme.BackgroundColor = theme.BackgroundColor;
            _configurationService.Theme.BackgroundTextColor = theme.BackgroundTextColor;

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

            _themeService.ApplyTheme(_configurationService.Theme);
        }

        public string ThemeName
        {
            get => _configurationService.Theme.ThemeName;
            set
            {
                Debug.Assert(_themeService.Themes.ContainsKey(value));
                if (SetAndNotifyIfChanged(ref _configurationService.Theme.ThemeName, value))
                {
                    SetThemeByName(value);
                    _configurationService.Save();
                }
            }
        }

        public List<string> ThemeList => _themeService.Themes.Select(x => x.Key).ToList();


        public string PrimaryMidColor
        {
            get => _configurationService.Theme.PrimaryMidColor;
            set
            {
                try
                {
                    if (SetAndNotifyIfChanged(ref _configurationService.Theme.PrimaryMidColor, value))
                    {
                        var color = ColorAndBrushHelper.HexColorToMediaColor(value);
                        _configurationService.Theme.PrimaryLightColor = System.Drawing.ColorTranslator.ToHtml(System.Drawing.Color.FromArgb(Math.Min(color.R + 50, 255), Math.Min(color.G + 45, 255), Math.Min(color.B + 40, 255)));
                        _configurationService.Theme.PrimaryDarkColor = System.Drawing.ColorTranslator.ToHtml(System.Drawing.Color.FromArgb((int)(color.R * 0.8), (int)(color.G * 0.8), (int)(color.B * 0.8)));
                        RaisePropertyChanged(nameof(PrimaryLightColor));
                        RaisePropertyChanged(nameof(PrimaryDarkColor));
                        _themeService.ApplyTheme(_configurationService.Theme);
                    }
                }
                catch (Exception e)
                {
                    SimpleLogHelper.Debug(e);
                }
            }
        }
        public string PrimaryLightColor
        {
            get => _configurationService.Theme.PrimaryLightColor;
            set
            {
                SetAndNotifyIfChanged(ref _configurationService.Theme.PrimaryLightColor, value);
                _themeService.ApplyTheme(_configurationService.Theme);
            }
        }

        public string PrimaryDarkColor
        {
            get => _configurationService.Theme.PrimaryDarkColor;
            set
            {
                SetAndNotifyIfChanged(ref _configurationService.Theme.PrimaryDarkColor, value);
                _themeService.ApplyTheme(_configurationService.Theme);
            }
        }

        public string PrimaryTextColor
        {
            get => _configurationService.Theme.PrimaryTextColor;
            set
            {
                SetAndNotifyIfChanged(ref _configurationService.Theme.PrimaryTextColor, value);
                _themeService.ApplyTheme(_configurationService.Theme);
            }
        }

        public string AccentMidColor
        {
            get => _configurationService.Theme.AccentMidColor;
            set
            {
                try
                {
                    if (SetAndNotifyIfChanged(ref _configurationService.Theme.AccentMidColor, value))
                    {
                        var color = ColorAndBrushHelper.HexColorToMediaColor(value);
                        _configurationService.Theme.AccentLightColor = System.Drawing.ColorTranslator.ToHtml(System.Drawing.Color.FromArgb(Math.Min(color.R + 50, 255), Math.Min(color.G + 45, 255), Math.Min(color.B + 40, 255)));
                        _configurationService.Theme.AccentDarkColor = System.Drawing.ColorTranslator.ToHtml(System.Drawing.Color.FromArgb((int)(color.R * 0.8), (int)(color.G * 0.8), (int)(color.B * 0.8)));
                        RaisePropertyChanged(nameof(AccentLightColor));
                        RaisePropertyChanged(nameof(AccentDarkColor));
                        _themeService.ApplyTheme(_configurationService.Theme);
                    }
                }
                catch (Exception e)
                {
                    SimpleLogHelper.Debug(e);
                }
            }
        }
        public string AccentLightColor
        {
            get => _configurationService.Theme.AccentLightColor;
            set
            {
                SetAndNotifyIfChanged(ref _configurationService.Theme.AccentLightColor, value);
                _themeService.ApplyTheme(_configurationService.Theme);
            }
        }

        public string AccentDarkColor
        {
            get => _configurationService.Theme.AccentDarkColor;
            set
            {
                SetAndNotifyIfChanged(ref _configurationService.Theme.AccentDarkColor, value);
                _themeService.ApplyTheme(_configurationService.Theme);
            }
        }

        public string AccentTextColor
        {
            get => _configurationService.Theme.AccentTextColor;
            set
            {
                SetAndNotifyIfChanged(ref _configurationService.Theme.AccentTextColor, value);
                _themeService.ApplyTheme(_configurationService.Theme);
            }
        }

        public string BackgroundColor
        {
            get => _configurationService.Theme.BackgroundColor;
            set
            {
                SetAndNotifyIfChanged(ref _configurationService.Theme.BackgroundColor, value);
                _themeService.ApplyTheme(_configurationService.Theme);
            }
        }

        public string BackgroundTextColor
        {
            get => _configurationService.Theme.BackgroundTextColor;
            set
            {
                SetAndNotifyIfChanged(ref _configurationService.Theme.BackgroundTextColor, value);
                _themeService.ApplyTheme(_configurationService.Theme);
            }
        }




        public class FontSizeOption
        {
            public string Name { get; set; }
            public int Size { get; set; }
        }

        public List<FontSizeOption> FontSizeOptions { get; set; } = new List<FontSizeOption>();
        public List<string> FontFamilies { get;  set; } = new();
        private void LoadSystemFonts()
        {
            FontFamilies = Fonts.SystemFontFamilies.OrderBy(f => f.Source).Select(fontFamily => fontFamily.Source).ToList();
            RaisePropertyChanged(nameof(FontFamilies));

            FontSizeOptions = new List<FontSizeOption>
            {
                new FontSizeOption { Name = "S", Size = 10 },
                new FontSizeOption { Name = "M", Size = 12 },
                new FontSizeOption { Name = "L", Size = 14 },
                new FontSizeOption { Name = "XL", Size = 16 },
                new FontSizeOption { Name = "XXL", Size = 18 },
                new FontSizeOption { Name = "XXXL", Size = 20 }
            };
            RaisePropertyChanged(nameof(FontSizeOptions));
        }

        public string FontFamily
        {
            get
            {
                var f = FontFamilies.FirstOrDefault(x => string.Equals(x, _configurationService.Theme.FontFamily, StringComparison.CurrentCultureIgnoreCase)) ??
                       FontFamilies.FirstOrDefault(x => x.EndsWith("YaHei", StringComparison.OrdinalIgnoreCase)) ??
                       FontFamilies.First();
                return f;
            }
            set
            {
                SetAndNotifyIfChanged(ref _configurationService.Theme.FontFamily, value);
                _themeService.ApplyTheme(_configurationService.Theme);
            }
        }

        public FontSizeOption FontSize
        {
            get
            {
                return FontSizeOptions.FirstOrDefault(x => x.Size == _configurationService.Theme.FontSize) ??
                       FontSizeOptions.First();
            }
            set
            {
                var v = value.Size;
                if (v < 10)
                    v = 10;
                if (v > 20)
                    v = 20;
                _configurationService.Theme.FontSize = v;
                RaisePropertyChanged();
                _themeService.ApplyTheme(_configurationService.Theme);
            }
        }


        private RelayCommand? _cmdPrmThemeReset;
        public RelayCommand CmdResetTheme
        {
            get
            {
                return _cmdPrmThemeReset ??= new RelayCommand((o) =>
                {
                    SetThemeByName(ThemeName);
                    _configurationService.Theme.FontSize = 12;
                    _configurationService.Theme.FontFamily = FontFamilies.FirstOrDefault(x => x.EndsWith("YaHei", StringComparison.OrdinalIgnoreCase)) ?? FontFamilies.First();
                    RaisePropertyChanged(nameof(FontSize));
                    RaisePropertyChanged(nameof(FontFamily));

                    _configurationService.Save();
                    _themeService.ApplyTheme(_configurationService.Theme);
                });
            }
        }
    }
}
