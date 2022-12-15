using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Shawn.Utils.Wpf;

namespace _1RM.Service
{
    public class ThemeService
    {
        private readonly ResourceDictionary _appResourceDictionary;
        public ThemeConfig CurrentTheme;
        public Dictionary<string, ThemeConfig> Themes { get; } = new Dictionary<string, ThemeConfig>();
        public ThemeService(ResourceDictionary appResourceDictionary, ThemeConfig defaultTheme)
        {
            _appResourceDictionary = appResourceDictionary;
            Themes.Add("Light", new ThemeConfig()
            {
                ThemeName = "Light",
                PrimaryMidColor = "#FFF2F3F5",
                PrimaryLightColor = "#FFFFFFFF",
                PrimaryDarkColor = "#FFE4E7EB",
                PrimaryTextColor = "#FF232323",
                AccentMidColor = "#FFE83D61",
                AccentLightColor = "#FFED6884",
                AccentDarkColor = "#FFB5304C",
                AccentTextColor = "#FFFFFFFF",
                BackgroundColor = "#FFFFFFFF",
                BackgroundTextColor = "#000000",
            });
            Themes.Add("Dark", new ThemeConfig()
            {
                ThemeName = "Dark",
                PrimaryMidColor = "#323233",
                PrimaryLightColor = "#474748",
                PrimaryDarkColor = "#2d2d2d",
                PrimaryTextColor = "#cccccc",
                AccentMidColor = "#FF007ACC",
                AccentLightColor = "#FF32A7F4",
                AccentDarkColor = "#FF0061A3",
                AccentTextColor = "#FFFFFFFF",
                BackgroundColor = "#1e1e1e",
                BackgroundTextColor = "#cccccc",
            });
            Themes.Add("PRemoteM", new ThemeConfig()
            {
                ThemeName = "PRemoteM",
                PrimaryMidColor = "#102b3e",
                PrimaryLightColor = "#445a68",
                PrimaryDarkColor = "#0c2230",
                PrimaryTextColor = "#FFFFFFFF",
                AccentMidColor = "#FFE83D61",
                AccentLightColor = "#FFED6884",
                AccentDarkColor = "#FFB5304C",
                AccentTextColor = "#FFFFFFFF",
                BackgroundColor = "#ced8e1",
                BackgroundTextColor = "#000000",
            });
            Themes.Add("SecretKey", new ThemeConfig()
            {
                ThemeName = "Light",
                PrimaryMidColor = "#FF473368",
                PrimaryLightColor = "#796090",
                PrimaryDarkColor = "#382853",
                PrimaryTextColor = "#FFFFFFFF",
                AccentMidColor = "#FFEF6D3B",
                AccentLightColor = "#FF9A63",
                AccentDarkColor = "#BF572F",
                AccentTextColor = "#FFFFFFFF",
                BackgroundColor = "#FFF2F1EC",
                BackgroundTextColor = "#000000",
            });
            Themes.Add("Greystone", new ThemeConfig()
            {
                ThemeName = "Greystone",
                PrimaryMidColor = "#FFC7D0D5",
                PrimaryLightColor = "#F9FDFD",
                PrimaryDarkColor = "#9FA6AA",
                PrimaryTextColor = "#FF1B2C3F",
                AccentMidColor = "#FFFF7247",
                AccentLightColor = "#FFED583A",
                AccentDarkColor = "#CC5B38",
                AccentTextColor = "#FFFFFFFF",
                BackgroundColor = "#FFF5F5F5",
                BackgroundTextColor = "#000000",
            });
            Themes.Add("Asphalt", new ThemeConfig()
            {
                ThemeName = "Asphalt",
                PrimaryMidColor = "#FF393939",
                PrimaryLightColor = "#6B6661",
                PrimaryDarkColor = "#2D2D2D",
                PrimaryTextColor = "#FFFFFFFF",
                AccentMidColor = "#FFFF7247",
                AccentLightColor = "#FFED583A",
                AccentDarkColor = "#CC5B38",
                AccentTextColor = "#FFFFFFFF",
                BackgroundColor = "#FFF5F5F5",
                BackgroundTextColor = "#000000",
            });
            Themes.Add("Wine", new ThemeConfig()
            {
                ThemeName = "Wine",
                PrimaryMidColor = "#FF57112D",
                PrimaryLightColor = "#893E55",
                PrimaryDarkColor = "#450D24",
                PrimaryTextColor = "#FFFFFFFF",
                AccentMidColor = "#FFA82159",
                AccentLightColor = "#DA4E81",
                AccentDarkColor = "#861A47",
                AccentTextColor = "#FFFFFFFF",
                BackgroundColor = "#FFFDEAD9",
                BackgroundTextColor = "#FF450D24",
            });
            Themes.Add("Forest", new ThemeConfig()
            {
                ThemeName = "Forest",
                PrimaryMidColor = "#FF253938",
                PrimaryLightColor = "#576660",
                PrimaryDarkColor = "#1D2D2C",
                PrimaryTextColor = "#FFFFFFFF",
                AccentMidColor = "#FF5FA291",
                AccentLightColor = "#91CFB9",
                AccentDarkColor = "#4C8174",
                AccentTextColor = "#FFFFFFFF",
                BackgroundColor = "#FFF5F5F5",
                BackgroundTextColor = "#FF303030",
            });
            Themes.Add("Soil", new ThemeConfig()
            {
                ThemeName = "Soil",
                PrimaryMidColor = "#FF776245",
                PrimaryLightColor = "#A98F6D",
                PrimaryDarkColor = "#FF735E41",
                PrimaryTextColor = "#FFFFFFFF",
                AccentMidColor = "#FF0193B8",
                AccentLightColor = "#33C0E0",
                AccentDarkColor = "#007593",
                AccentTextColor = "#FFFFFFFF",
                BackgroundColor = "#FFCFC3B5",
                BackgroundTextColor = "#FF080000",
            });

            CurrentTheme = defaultTheme;
            ApplyTheme(defaultTheme);
        }

        public void ApplyTheme(ThemeConfig theme)
        {
            const string resourceTypeKey = "__Resource_Type_Key";
            const string resourceTypeValue = "__Resource_Type_Value=theme";
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

            // create new theme resources
            var rd = new ResourceDictionary();
            SetKey(rd, resourceTypeKey, resourceTypeValue);
            SetKey(rd, "PrimaryMidColor", ColorAndBrushHelper.HexColorToMediaColor(theme.PrimaryMidColor));
            SetKey(rd, "PrimaryLightColor", ColorAndBrushHelper.HexColorToMediaColor(theme.PrimaryLightColor));
            SetKey(rd, "PrimaryDarkColor", ColorAndBrushHelper.HexColorToMediaColor(theme.PrimaryDarkColor));
            SetKey(rd, "PrimaryTextColor", ColorAndBrushHelper.HexColorToMediaColor(theme.PrimaryTextColor));
            SetKey(rd, "AccentMidColor", ColorAndBrushHelper.HexColorToMediaColor(theme.AccentMidColor));
            SetKey(rd, "AccentLightColor", ColorAndBrushHelper.HexColorToMediaColor(theme.AccentLightColor));
            SetKey(rd, "AccentDarkColor", ColorAndBrushHelper.HexColorToMediaColor(theme.AccentDarkColor));
            SetKey(rd, "AccentTextColor", ColorAndBrushHelper.HexColorToMediaColor(theme.AccentTextColor));
            SetKey(rd, "BackgroundColor", ColorAndBrushHelper.HexColorToMediaColor(theme.BackgroundColor));
            SetKey(rd, "BackgroundTextColor", ColorAndBrushHelper.HexColorToMediaColor(theme.BackgroundTextColor));


            SetKey(rd, "PrimaryMidBrush", ColorAndBrushHelper.ColorToMediaBrush(theme.PrimaryMidColor));
            SetKey(rd, "PrimaryLightBrush", ColorAndBrushHelper.ColorToMediaBrush(theme.PrimaryLightColor));
            SetKey(rd, "PrimaryDarkBrush", ColorAndBrushHelper.ColorToMediaBrush(theme.PrimaryDarkColor));
            SetKey(rd, "PrimaryTextBrush", ColorAndBrushHelper.ColorToMediaBrush(theme.PrimaryTextColor));
            SetKey(rd, "AccentMidBrush", ColorAndBrushHelper.ColorToMediaBrush(theme.AccentMidColor));
            SetKey(rd, "AccentLightBrush", ColorAndBrushHelper.ColorToMediaBrush(theme.AccentLightColor));
            SetKey(rd, "AccentDarkBrush", ColorAndBrushHelper.ColorToMediaBrush(theme.AccentLightColor));
            SetKey(rd, "AccentTextBrush", ColorAndBrushHelper.ColorToMediaBrush(theme.AccentTextColor));
            SetKey(rd, "BackgroundBrush", ColorAndBrushHelper.ColorToMediaBrush(theme.BackgroundColor));
            SetKey(rd, "BackgroundTextBrush", ColorAndBrushHelper.ColorToMediaBrush(theme.BackgroundTextColor));

            SetKey(rd, "PrimaryColor", ColorAndBrushHelper.HexColorToMediaColor(theme.AccentMidColor));
            SetKey(rd, "DarkPrimaryColor", ColorAndBrushHelper.HexColorToMediaColor(theme.AccentDarkColor));
            SetKey(rd, "PrimaryDarkColor", ColorAndBrushHelper.HexColorToMediaColor(theme.AccentTextColor));

            foreach (var r in rs)
            {
                _appResourceDictionary.MergedDictionaries.Remove(r);
            }
            _appResourceDictionary.MergedDictionaries.Add(rd);
        }
    }
}
