using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using ColorPickerWPF.Code;
using Shawn.Utils;
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
            ((ColorPickRowPopup)d).HexColor = ColorAndBrushHelper.ArgbToHexColor(color.A, color.R, color.G, color.B); ;
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
                var c = ColorAndBrushHelper.HexColorToMediaColor(value);
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
                    var c = ColorAndBrushHelper.HexColorToMediaColor(value);
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
    }
}
