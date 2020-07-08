using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PRM.Core.Resources.Theme
{
    public class PrmTheme
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

    public static class PrmThemes
    {
    }
}
