using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using ColorPickerWPF.Code;
using UserControl = System.Windows.Controls.UserControl;

namespace ColorPickerWPF
{
    /// <summary>
    /// Interaction logic for ColorPickerSwatch.xaml
    /// </summary>
    public partial class ColorPickerSwatch : UserControl
    {
        public delegate void ColorSwatchPickHandler(Color color);

        public event ColorSwatchPickHandler OnPickColor;

        public bool Editable { get; set; }
        public Color CurrentColor = Colors.White;

        public ColorPickerSwatch()
        {
            InitializeComponent();
        }


        private void UIElement_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            var border = (sender as Border);
            if (border == null)
                return;

            if (Editable && Keyboard.IsKeyDown(Key.LeftCtrl))
            {
                border.Background = new SolidColorBrush(CurrentColor);

                var data = border.DataContext as ColorSwatchItem;
                if (data != null)
                {
                    data.Color = CurrentColor;
                    data.HexString = CurrentColor.ToHexString();
                }
            }
            else
            {
                var color = border.Background as SolidColorBrush;
                OnPickColor?.Invoke(color.Color);
            }

            
        }


        internal List<ColorSwatchItem> GetColors()
        {
            var results = new List<ColorSwatchItem>();

            var shit = SwatchListBox.Items;

            var colors = SwatchListBox.ItemsSource as List<ColorSwatchItem>;
            if (colors != null)
            {
                return colors;
            }

            return results;
        }
    }
}
