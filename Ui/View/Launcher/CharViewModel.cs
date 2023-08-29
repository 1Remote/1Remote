using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Shawn.Utils;

namespace _1RM.View.Launcher
{
    public class CharViewModel : NotifyPropertyChangedBase
    {
        public static SolidColorBrush HighLightBrush = new SolidColorBrush(Color.FromArgb(80, 239, 242, 132));

        public CharViewModel(char c)
        {
            Char = c;
        }

        public char Char { get; }
        public bool IsHighLight { get; set; }
        public SolidColorBrush BackgroundBrush => IsHighLight ? HighLightBrush : Brushes.Transparent;
    }
}
