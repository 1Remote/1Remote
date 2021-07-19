using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ColorPickerWPF.Code;
using Shawn.Utils;
using UserControl = System.Windows.Controls.UserControl;

namespace ColorPickerWPF
{
    public class ColorSwatchItem
    {
        public Color Color { get; set; }
        public string HexString { get; set; }
    }


    /// <summary>
    /// Interaction logic for ColorPickerControl.xaml
    /// </summary>
    public partial class ColorPickerControl4Popup : UserControl
    {
        public Color Color = Colors.White;

        public delegate void ColorPickerChangeHandler(Color color);

        public event ColorPickerChangeHandler OnPickColor;

        internal List<Brush> ColorSwatches = new List<Brush>();

        public bool IsSettingValues = false;

        protected const int NumColorsFirstSwatch = 13 * 4;

        public ColorPickerControl4Popup()
        {
            InitializeComponent();

            ColorSwatches.AddRange(GetColors().Take(NumColorsFirstSwatch).ToArray());
            Swatch1.ItemsSource = ColorSwatches;

            RSlider.Slider.Maximum = 255;
            GSlider.Slider.Maximum = 255;
            BSlider.Slider.Maximum = 255;
            ASlider.Slider.Maximum = 255;
            HSlider.Slider.Maximum = 360;
            SSlider.Slider.Maximum = 1;
            LSlider.Slider.Maximum = 1;


            RSlider.Label.Text = "R";
            RSlider.Slider.TickFrequency = 1;
            RSlider.Slider.IsSnapToTickEnabled = true;
            GSlider.Label.Text = "G";
            GSlider.Slider.TickFrequency = 1;
            GSlider.Slider.IsSnapToTickEnabled = true;
            BSlider.Label.Text = "B";
            BSlider.Slider.TickFrequency = 1;
            BSlider.Slider.IsSnapToTickEnabled = true;

            ASlider.Label.Text = "A";
            ASlider.Slider.TickFrequency = 1;
            ASlider.Slider.IsSnapToTickEnabled = true;

            HSlider.Label.Text = "H";
            HSlider.Slider.TickFrequency = 1;
            HSlider.Slider.IsSnapToTickEnabled = true;
            SSlider.Label.Text = "S";
            //SSlider.Slider.TickFrequency = 1;
            //SSlider.Slider.IsSnapToTickEnabled = true;
            LSlider.Label.Text = "V";
            //LSlider.Slider.TickFrequency = 1;
            //LSlider.Slider.IsSnapToTickEnabled = true;


            SetColor(Color);

        }


        protected internal List<Brush> GetColors()
        {
            return new List<Brush>()
            {
                Brushes.Black,
                Brushes.Red,
                Brushes.DarkOrange,
                Brushes.Yellow,
                Brushes.LawnGreen,
                Brushes.Blue,
                Brushes.Purple,
                Brushes.DeepPink,
                Brushes.Aqua,
                Brushes.SaddleBrown,
                Brushes.Wheat,
                Brushes.BurlyWood,
                Brushes.Teal,

                Brushes.White,
                Brushes.OrangeRed,
                Brushes.Orange,
                Brushes.Gold,
                Brushes.LimeGreen,
                Brushes.DodgerBlue,
                Brushes.Orchid,
                Brushes.HotPink,
                Brushes.Turquoise,
                Brushes.SandyBrown,
                Brushes.SeaGreen,
                Brushes.SlateBlue,
                Brushes.RoyalBlue,

                Brushes.Tan,
                Brushes.Peru,
                Brushes.DarkBlue,
                Brushes.DarkGreen,
                Brushes.DarkSlateBlue,
                Brushes.Navy,
                Brushes.MistyRose,
                Brushes.LemonChiffon,
                Brushes.ForestGreen,
                Brushes.Firebrick,
                Brushes.DarkViolet,
                Brushes.Aquamarine,
                Brushes.CornflowerBlue,


                new SolidColorBrush(Color.FromArgb(255, 5, 5, 5)),
                new SolidColorBrush(Color.FromArgb(255, 35, 35, 35)),
                new SolidColorBrush(Color.FromArgb(255, 55, 55, 55)),
                new SolidColorBrush(Color.FromArgb(255, 75, 75, 75)),
                new SolidColorBrush(Color.FromArgb(255, 95, 95, 95)),
                new SolidColorBrush(Color.FromArgb(255, 115, 115, 115)),
                new SolidColorBrush(Color.FromArgb(255, 135, 135, 135)),
                new SolidColorBrush(Color.FromArgb(255, 155, 155, 155)),
                new SolidColorBrush(Color.FromArgb(255, 175, 175, 175)),
                new SolidColorBrush(Color.FromArgb(255, 195, 195, 195)),
                new SolidColorBrush(Color.FromArgb(255, 215, 215, 215)),
                new SolidColorBrush(Color.FromArgb(255, 235, 235, 235)),
                new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
            };
        }

        public void SetColor(Color color)
        {
            Color = color;

            IsSettingValues = true;

            RSlider.Slider.Value = Color.R;
            GSlider.Slider.Value = Color.G;
            BSlider.Slider.Value = Color.B;
            ASlider.Slider.Value = Color.A;

            SSlider.Slider.Value = Color.GetSaturation();
            LSlider.Slider.Value = Color.GetBrightness();
            HSlider.Slider.Value = Color.GetHue();

            ColorDisplayBorder.Background = new SolidColorBrush(Color);

            IsSettingValues = false;
            OnPickColor?.Invoke(color);
            TbHex.Text = color.ToHexString();
            if (TbHex.IsFocused)
                TbHex.CaretIndex = TbHex.Text.Length;
        }


        protected void SampleImageClick(BitmapSource img, Point pos)
        {
            // https://social.msdn.microsoft.com/Forums/vstudio/en-US/82a5731e-e201-4aaf-8d4b-062b138338fe/getting-pixel-information-from-a-bitmapimage?forum=wpf

            int stride = (int)img.Width * 4;
            int size = (int)img.Height * stride;
            byte[] pixels = new byte[(int)size];

            img.CopyPixels(pixels, stride, 0);


            // Get pixel
            var x = (int)pos.X;
            var y = (int)pos.Y;

            int index = y * stride + 4 * x;

            byte b = pixels[index];
            byte g = pixels[index + 1];
            byte r = pixels[index + 2];
            byte a = pixels[index + 3];

            var color = Color.FromArgb(a, r, g, b);
            SetColor(color);
        }


        private void SampleImage_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            Mouse.Capture(this);

            this.MouseMove += ColorPickerControl_MouseMove;
            this.MouseUp += ColorPickerControl_MouseUp;
        }

        private void Swatch_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            var border = (sender as Border);
            if (border == null)
                return;
            var color = border.Background as SolidColorBrush;
            OnPickColor?.Invoke(color.Color);
        }

        private void ColorPickerControl_MouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(SampleImage);
            var img = SampleImage.Source as BitmapSource;

            if (pos.X > 0 && pos.Y > 0 && pos.X < img.PixelWidth && pos.Y < img.PixelHeight)
                SampleImageClick(img, pos);
        }

        private void ColorPickerControl_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Mouse.Capture(null);
            this.MouseMove -= ColorPickerControl_MouseMove;
            this.MouseUp -= ColorPickerControl_MouseUp;
        }

        private void HSlider_OnOnValueChanged(double value)
        {
            if (!IsSettingValues)
            {
                var s = Color.GetSaturation();
                var l = Color.GetBrightness();
                var h = (float)value;
                var a = (int)ASlider.Slider.Value;
                Color = Util.FromAhsb(a, h, s, l);

                SetColor(Color);
            }
        }




        private void RSlider_OnOnValueChanged(double value)
        {
            if (!IsSettingValues)
            {
                var val = (byte)value;
                Color.R = val;
                SetColor(Color);
            }
        }

        private void GSlider_OnOnValueChanged(double value)
        {
            if (!IsSettingValues)
            {
                var val = (byte)value;
                Color.G = val;
                SetColor(Color);
            }
        }

        private void BSlider_OnOnValueChanged(double value)
        {
            if (!IsSettingValues)
            {
                var val = (byte)value;
                Color.B = val;
                SetColor(Color);
            }
        }

        private void ASlider_OnOnValueChanged(double value)
        {
            if (!IsSettingValues)
            {
                var val = (byte)value;
                Color.A = val;
                SetColor(Color);
            }
        }

        private void SSlider_OnOnValueChanged(double value)
        {
            if (!IsSettingValues)
            {
                var s = (float)value;
                var l = Color.GetBrightness();
                var h = Color.GetHue();
                var a = (int)ASlider.Slider.Value;
                Color = Util.FromAhsb(a, h, s, l);

                SetColor(Color);
            }

        }

        private void LSlider_OnOnValueChanged(double value)
        {
            if (!IsSettingValues)
            {
                var s = Color.GetSaturation();
                var l = (float)value;
                var h = Color.GetHue();
                var a = (int)ASlider.Slider.Value;
                Color = Util.FromAhsb(a, h, s, l);
                SetColor(Color);
            }
        }

        private void TbHex_OnKeyUp(object sender, KeyEventArgs e)
        {
            try
            {
                TbHex.Text = TbHex.Text.Trim();
                if (TbHex.Text.TrimStart('#').Length == 8 && 
                    !string.Equals(Color.ToHexString(), TbHex.Text, StringComparison.CurrentCultureIgnoreCase))
                {
                    var (a, r, g, b) = ColorAndBrushHelper.HexColorToArgb(TbHex.Text);
                    var color = Color.FromArgb(a, r, g, b);
                    SetColor(color);
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }
        }
    }
}
