using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PRM.Core.Resources.Theme
{
    public class PrmColorTheme
    {
        public string Name { get; set; }
        public string PrimaryMidColor { get; set; }
        public string PrimaryLightColor { get; set; }
        public string PrimaryDarkColor { get; set; }
        public string PrimaryTextColor { get; set; }
        public string AccentMidColor { get; set; }
        public string AccentLightColor { get; set; }
        public string AccentDarkColor { get; set; }
        public string AccentTextColor { get; set; }
        public string BackgroundColor { get; set; }
        public string BackgroundTextColor { get; set; }
    }

    public static class PrmColorThemes
    {
        public static Dictionary<string, PrmColorTheme> GetThemes()
        {
            var ret = new Dictionary<string, PrmColorTheme>();
            AddStaticThemes(ret);
            return ret;
        }

        private static void AddOneTheme(Dictionary<string, PrmColorTheme> themes, KeyValuePair<string, PrmColorTheme> theme)
        {
            if (!themes.ContainsKey(theme.Key))
                themes.Add(theme.Key, theme.Value);
        }

        private static void AddStaticThemes(Dictionary<string, PrmColorTheme> themes)
        {
            AddOneTheme(themes, GetDefault());
            AddOneTheme(themes, GetSecretKey());
            AddOneTheme(themes, GetGreystone());
            AddOneTheme(themes, GetAsphalt());
            AddOneTheme(themes, GetWine());
            AddOneTheme(themes, GetForest());
            AddOneTheme(themes, GetSoil());
        }

        public static KeyValuePair<string, PrmColorTheme> GetDefault()
        {
            return new KeyValuePair<string, PrmColorTheme>("Default", new PrmColorTheme()
            {
                PrimaryMidColor = "#102b3e",
                PrimaryLightColor = "#445a68",
                PrimaryDarkColor = "#0c2230",
                PrimaryTextColor = "#FFFFFFFF",
                AccentMidColor = "#e83d61",
                AccentLightColor = "#ed6884",
                AccentDarkColor = "#b5304c",
                AccentTextColor = "#FFFFFFFF",
                BackgroundColor = "#ced8e1",
                BackgroundTextColor = "#000000",
            });
        }

        public static KeyValuePair<string, PrmColorTheme> GetSecretKey()
        {
            return new KeyValuePair<string, PrmColorTheme>("SecretKey", new PrmColorTheme()
            {
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
        }

        public static KeyValuePair<string, PrmColorTheme> GetGreystone()
        {
            return new KeyValuePair<string, PrmColorTheme>("Greystone", new PrmColorTheme()
            {
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
        }

        public static KeyValuePair<string, PrmColorTheme> GetAsphalt()
        {
            return new KeyValuePair<string, PrmColorTheme>("Asphalt", new PrmColorTheme()
            {
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
        }

        public static KeyValuePair<string, PrmColorTheme> GetWine()
        {
            return new KeyValuePair<string, PrmColorTheme>("Wine", new PrmColorTheme()
            {
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
        }
        public static KeyValuePair<string, PrmColorTheme> GetForest()
        {
            return new KeyValuePair<string, PrmColorTheme>("Forest", new PrmColorTheme()
            {
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
        }
        public static KeyValuePair<string, PrmColorTheme> GetSoil()
        {
            return new KeyValuePair<string, PrmColorTheme>("Soil", new PrmColorTheme()
            {
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
        }
    }
}
