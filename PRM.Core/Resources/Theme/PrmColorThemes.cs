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

        private static void AddStaticThemes(Dictionary<string, PrmColorTheme> themes)
        {
            {
                var theme = GetDefault();
                if (!themes.ContainsKey(theme.Key))
                    themes.Add(theme.Key, theme.Value);
            }
        }

        public static KeyValuePair<string, PrmColorTheme> GetDefault()
        {
            return new KeyValuePair<string, PrmColorTheme>("Default", new PrmColorTheme()
            {
                MainColor1 = "#102b3e",
                MainColor1Lighter = "#445a68",
                MainColor1Darker = "#0c2230",
                MainColor1Foreground = "#ffffff",
                MainColor2 = "#e83d61",
                MainColor2Lighter = "#ed6884",
                MainColor2Darker = "#b5304c",
                MainColor2Foreground = "#ffffff",
                MainBgColor = "#ced8e1",
                MainBgColorForeground = "#000000",
            });
        }
    }
}
