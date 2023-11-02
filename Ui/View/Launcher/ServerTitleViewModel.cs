using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Shawn.Utils;
using System.Windows.Media;

namespace _1RM.View.Launcher
{
    public class ServerTitleViewModel : NotifyPropertyChangedBase
    {
        public static readonly SolidColorBrush HighLightBrush = new SolidColorBrush(Color.FromArgb(80, 239, 242, 132));

        public ServerTitleViewModel(string text)
        {
            Text = text;
        }

        public string Text { get; }

        private Visibility _textVisibility;
        public Visibility TextVisibility
        {
            get => _textVisibility;
            set => SetAndNotifyIfChanged(ref _textVisibility, value);
        }

        private Visibility _highlightVisibility;
        public Visibility HighlightVisibility
        {
            get => _highlightVisibility;
            set => SetAndNotifyIfChanged(ref _highlightVisibility, value);
        }

        private object? _displayNameControl = null;
        public object? DisplayNameControl
        {
            get => _displayNameControl;
            set => SetAndNotifyIfChanged(ref _displayNameControl, value);
        }


        public void UnHighLightAll()
        {
            TextVisibility = Visibility.Visible;
            HighlightVisibility = Visibility.Collapsed;
        }
        public void HighLight(List<bool> flags)
        {
            if (flags.Count == Text.Length && flags.Any(x => x))
            {
                var sp = new StackPanel() { Orientation = System.Windows.Controls.Orientation.Horizontal };
                for (int i = 0; i < flags.Count; i++)
                {
                    int j = i + 1;
                    for (; j < flags.Count; j++)
                    {
                        if (flags[j] != flags[i])
                            break;
                    }
                    var text = Text.Substring(i, j - i);
                    if (flags[i])
                        sp.Children.Add(new TextBlock()
                        {
                            Text = text,
                            Background = HighLightBrush,
                        });
                    else
                        sp.Children.Add(new TextBlock()
                        {
                            Text = text,
                        });
                    i = j - 1;
                }
                DisplayNameControl = sp;
                TextVisibility = Visibility.Collapsed;
                HighlightVisibility = Visibility.Visible;
            }
            else
            {
                UnHighLightAll();
            }
        }
    }
}
