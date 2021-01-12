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
        public string MainColor1 { get; set; }
        public string MainColor1Lighter { get; set; }
        public string MainColor1Darker { get; set; }
        public string MainColor1Foreground { get; set; }
        public string MainColor2 { get; set; }
        public string MainColor2Lighter { get; set; }
        public string MainColor2Darker { get; set; }
        public string MainColor2Foreground { get; set; }
        public string MainBgColor { get; set; }
        public string MainBgColorForeground { get; set; }
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
                MainColor1 = "#102b3e",
                MainColor1Lighter = "#445a68",
                MainColor1Darker = "#0c2230",
                MainColor1Foreground = "#FFFFFFFF",
                MainColor2 = "#e83d61",
                MainColor2Lighter = "#ed6884",
                MainColor2Darker = "#b5304c",
                MainColor2Foreground = "#FFFFFFFF",
                MainBgColor = "#ced8e1",
                MainBgColorForeground = "#000000",
            });
        }

        public static KeyValuePair<string, PrmColorTheme> GetSecretKey()
        {
            return new KeyValuePair<string, PrmColorTheme>("SecretKey", new PrmColorTheme()
            {
                MainColor1 = "#FF473368",
                MainColor1Lighter = "#796090",
                MainColor1Darker = "#382853",
                MainColor1Foreground = "#FFFFFFFF",
                MainColor2 = "#FFEF6D3B",
                MainColor2Lighter = "#FF9A63",
                MainColor2Darker = "#BF572F",
                MainColor2Foreground = "#FFFFFFFF",
                MainBgColor = "#FFF2F1EC",
                MainBgColorForeground = "#000000",
            });
        }

        public static KeyValuePair<string, PrmColorTheme> GetGreystone()
        {
            return new KeyValuePair<string, PrmColorTheme>("Greystone", new PrmColorTheme()
            {
                MainColor1 = "#FFC7D0D5",
                MainColor1Lighter = "#F9FDFD",
                MainColor1Darker = "#9FA6AA",
                MainColor1Foreground = "#FF1B2C3F",
                MainColor2 = "#FFFF7247",
                MainColor2Lighter = "#FFED583A",
                MainColor2Darker = "#CC5B38",
                MainColor2Foreground = "#FFFFFFFF",
                MainBgColor = "#FFF5F5F5",
                MainBgColorForeground = "#000000",
            });
        }

        public static KeyValuePair<string, PrmColorTheme> GetAsphalt()
        {
            return new KeyValuePair<string, PrmColorTheme>("Asphalt", new PrmColorTheme()
            {
                MainColor1 = "#FF393939",
                MainColor1Lighter = "#6B6661",
                MainColor1Darker = "#2D2D2D",
                MainColor1Foreground = "#FFFFFFFF",
                MainColor2 = "#FFFF7247",
                MainColor2Lighter = "#FFED583A",
                MainColor2Darker = "#CC5B38",
                MainColor2Foreground = "#FFFFFFFF",
                MainBgColor = "#FFF5F5F5",
                MainBgColorForeground = "#000000",
            });
        }

        public static KeyValuePair<string, PrmColorTheme> GetWine()
        {
            return new KeyValuePair<string, PrmColorTheme>("Wine", new PrmColorTheme()
            {
                MainColor1 = "#FF57112D",
                MainColor1Lighter = "#893E55",
                MainColor1Darker = "#450D24",
                MainColor1Foreground = "#FFFFFFFF",
                MainColor2 = "#FFA82159",
                MainColor2Lighter = "#DA4E81",
                MainColor2Darker = "#861A47",
                MainColor2Foreground = "#FFFFFFFF",
                MainBgColor = "#FFFDEAD9",
                MainBgColorForeground = "#FF450D24",
            });
        }
        public static KeyValuePair<string, PrmColorTheme> GetForest()
        {
            return new KeyValuePair<string, PrmColorTheme>("Forest", new PrmColorTheme()
            {
                MainColor1 = "#FF253938",
                MainColor1Lighter = "#576660",
                MainColor1Darker = "#1D2D2C",
                MainColor1Foreground = "#FFFFFFFF",
                MainColor2 = "#FF5FA291",
                MainColor2Lighter = "#91CFB9",
                MainColor2Darker = "#4C8174",
                MainColor2Foreground = "#FFFFFFFF",
                MainBgColor = "#FFF5F5F5",
                MainBgColorForeground = "#FF303030",
            });
        }
        public static KeyValuePair<string, PrmColorTheme> GetSoil()
        {
            return new KeyValuePair<string, PrmColorTheme>("Soil", new PrmColorTheme()
            {
                MainColor1 = "#FF776245",
                MainColor1Lighter = "#A98F6D",
                MainColor1Darker = "#FF735E41",
                MainColor1Foreground = "#FFFFFFFF",
                MainColor2 = "#FF0193B8",
                MainColor2Lighter = "#33C0E0",
                MainColor2Darker = "#007593",
                MainColor2Foreground = "#FFFFFFFF",
                MainBgColor = "#FFCFC3B5",
                MainBgColorForeground = "#FF080000",
            });
        }
    }
}
