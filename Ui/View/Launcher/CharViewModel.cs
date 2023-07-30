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

        private bool _isHighLight = false;
        public bool IsHighLight
        {
            get => _isHighLight;
            set
            {
                if (SetAndNotifyIfChanged(ref _isHighLight, value))
                {
                    RaisePropertyChanged(nameof(BackgroundBrush));
                }
            }
        }

        public SolidColorBrush BackgroundBrush => IsHighLight ? HighLightBrush : Brushes.Transparent;
    }
}
