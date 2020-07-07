using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using ColorPickerWPF.Code;
using Color = System.Windows.Media.Color;
using UserControl = System.Windows.Controls.UserControl;

namespace ColorPickerWPF
{
    /// <summary>
    /// Interaction logic for ColorPickRowPopup.xaml
    /// </summary>
    public partial class ColorPickRowPopup : UserControl
    {
        #region Color
        public static readonly DependencyProperty ColorProperty =
            DependencyProperty.Register("Color", typeof(Color), typeof(ColorPickRowPopup),
                    new FrameworkPropertyMetadata(Colors.White, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnColorPropertyChanged));

        private static void OnColorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var color = (Color)e.NewValue;
            ((ColorPickRowPopup)d).HexColor = ArgbToHexString(color.A, color.R, color.G, color.B); ;
        }

        public Color Color
        {
            get => (Color)GetValue(ColorProperty);
            set
            {
                if (value == Color)
                    return;
                var c = value;
                var hexColor = value.ToHexString();
                if (c != Color)
                    SetValue(ColorProperty, c);
                if (HexColor != hexColor)
                    SetValue(HexColorProperty, hexColor);
                ColorDisplayGrid.Background = new SolidColorBrush(c);
                ColorPicker.SetColor(c);
            }
        }
        #endregion


        #region HexColor
        public static readonly DependencyProperty HexColorProperty =
            DependencyProperty.Register("HexColor", typeof(string), typeof(ColorPickRowPopup),
                new FrameworkPropertyMetadata("#FFFFFFFF", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHexColorPropertyChanged));
        private static void OnHexColorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var value = (string)e.NewValue;
            try
            {
                var c = ConvertStringToColor(value);
                ((ColorPickRowPopup)d).Color = c;
                ((ColorPickRowPopup)d).ColorDisplayGrid.Background = new SolidColorBrush(c);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
        public string HexColor
        {
            get => (string)GetValue(HexColorProperty);
            set
            {
                if (value == HexColor)
                    return;
                try
                {
                    var c = ConvertStringToColor(value);
                    var hexColor = value;
                    if (c != Color)
                        SetValue(ColorProperty, c);
                    if (HexColor != hexColor)
                        SetValue(HexColorProperty, hexColor);
                    ColorDisplayGrid.Background = new SolidColorBrush(c);
                    ColorPicker.SetColor(c);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }
        #endregion


        #region HexVisible
        public static readonly DependencyProperty HexVisibleProperty = DependencyProperty.Register("HexVisible", typeof(Visibility), typeof(ColorPickRowPopup),
            new PropertyMetadata(System.Windows.Visibility.Visible, null));
        public Visibility HexVisible
        {
            get => (Visibility)GetValue(HexVisibleProperty);
            set => SetValue(HexVisibleProperty, value);
        }
        #endregion


        public ColorPickRowPopup()
        {
            InitializeComponent();
            ColorPicker.OnPickColor += color => { HexColor = color.ToHexString(); };
        }

        private void PickColorButton_OnClick(object sender, RoutedEventArgs e)
        {
            PopupPicker.Focus();
            PopupPicker.IsOpen = true;
        }




        private static string ArgbToHexString(byte a, byte r, byte g, byte b)
        {
            return "#" + a.ToString("X2") + r.ToString("X2") + g.ToString("X2") + b.ToString("X2");
        }

        private static System.Windows.Media.Color ConvertStringToColor(string hex)
        {
            //remove the # at the front
            hex = hex.Replace("#", "");

            byte a = 255;
            byte r = 255;
            byte g = 255;
            byte b = 255;

            int start = 0;

            //handle ARGB strings (8 characters long)
            if (hex.Length == 8)
            {
                a = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                start = 2;
            }

            //convert RGB characters to bytes
            r = byte.Parse(hex.Substring(start, 2), System.Globalization.NumberStyles.HexNumber);
            g = byte.Parse(hex.Substring(start + 2, 2), System.Globalization.NumberStyles.HexNumber);
            b = byte.Parse(hex.Substring(start + 4, 2), System.Globalization.NumberStyles.HexNumber);

            return System.Windows.Media.Color.FromArgb(a, r, g, b);
        }
    }
}
