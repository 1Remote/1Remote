using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ColorPickerWPF.Code;
using Shawn.Utils;
using UserControl = System.Windows.Controls.UserControl;

namespace ColorPickerWPF
{
    /// <summary>
    /// Interaction logic for ColorPickerControl.xaml
    /// </summary>
    public partial class ColorPickerControl4Popup : UserControl
    {
        public Color Color = Colors.White;

        public delegate void ColorPickerChangeHandler(Color color);

        public event ColorPickerChangeHandler OnPickColor;

        internal List<ColorSwatchItem> ColorSwatch1 = new List<ColorSwatchItem>();
        internal List<ColorSwatchItem> ColorSwatch2 = new List<ColorSwatchItem>();

        public bool IsSettingValues = false;

        protected const int NumColorsFirstSwatch = 39;
        protected const int NumColorsSecondSwatch = 112;

        internal static ColorPalette ColorPalette;


        public ColorPickerControl4Popup()
        {
            InitializeComponent();

            if (ColorPalette == null)
            {
                ColorPalette = new ColorPalette();
                ColorPalette.InitializeDefaults();
            }


            ColorSwatch1.AddRange(ColorPalette.BuiltInColors.Take(NumColorsFirstSwatch).ToArray());

            ColorSwatch2.AddRange(ColorPalette.BuiltInColors.Skip(NumColorsFirstSwatch).Take(NumColorsSecondSwatch).ToArray());

            Swatch1.SwatchListBox.ItemsSource = ColorSwatch1;
            Swatch2.SwatchListBox.ItemsSource = ColorSwatch2;


            RSlider.Slider.Maximum = 255;
            GSlider.Slider.Maximum = 255;
            BSlider.Slider.Maximum = 255;
            ASlider.Slider.Maximum = 255;
            HSlider.Slider.Maximum = 360;
            SSlider.Slider.Maximum = 1;
            LSlider.Slider.Maximum = 1;


            RSlider.Label.Content = "R";
            RSlider.Slider.TickFrequency = 1;
            RSlider.Slider.IsSnapToTickEnabled = true;
            GSlider.Label.Content = "G";
            GSlider.Slider.TickFrequency = 1;
            GSlider.Slider.IsSnapToTickEnabled = true;
            BSlider.Label.Content = "B";
            BSlider.Slider.TickFrequency = 1;
            BSlider.Slider.IsSnapToTickEnabled = true;

            ASlider.Label.Content = "A";
            ASlider.Slider.TickFrequency = 1;
            ASlider.Slider.IsSnapToTickEnabled = true;

            HSlider.Label.Content = "H";
            HSlider.Slider.TickFrequency = 1;
            HSlider.Slider.IsSnapToTickEnabled = true;
            SSlider.Label.Content = "S";
            //SSlider.Slider.TickFrequency = 1;
            //SSlider.Slider.IsSnapToTickEnabled = true;
            LSlider.Label.Content = "V";
            //LSlider.Slider.TickFrequency = 1;
            //LSlider.Slider.IsSnapToTickEnabled = true;


            SetColor(Color);

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

        private void Swatch_OnOnPickColor(Color color)
        {
            SetColor(color);
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

        public void LoadCustomPalette(string filename)
        {
            if (File.Exists(filename))
            {
                try
                {
                    ColorPalette = ColorPalette.LoadFromXml(filename);


                    // Do regular one too

                    ColorSwatch1.Clear();
                    ColorSwatch2.Clear();
                    ColorSwatch1.AddRange(ColorPalette.BuiltInColors.Take(NumColorsFirstSwatch).ToArray());
                    ColorSwatch2.AddRange(ColorPalette.BuiltInColors.Skip(NumColorsFirstSwatch).Take(NumColorsSecondSwatch).ToArray());
                    Swatch1.SwatchListBox.ItemsSource = ColorSwatch1;
                    Swatch2.SwatchListBox.ItemsSource = ColorSwatch2;

                }
                catch (Exception ex)
                {
                }

            }
        }

        private void TbHex_OnKeyUp(object sender, KeyEventArgs e)
        {
            try
            {
                var (a, r, g, b) = HexColorToArgb(TbHex.Text);
                var color = Color.FromArgb(a, r, g, b);
                SetColor(color);
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }
        }
        /// <summary>
        /// color in hex string to (a,r,g,b);
        /// #FFFEFDFC   ->  Tuple(255,254,253,252),
        /// #FEFDFC     ->  Tuple(255,254,253,252)
        /// </summary>
        /// <param name="hexColor"></param>
        /// <returns></returns>
        private static Tuple<byte, byte, byte, byte> HexColorToArgb(string hexColor)
        {
            //remove the # at the front
            var hex = hexColor.Replace("#", "");

            byte a = 255;
            byte r = 255;
            byte g = 255;
            byte b = 255;

            int start = 0;

            try
            {
                if (hex.Length != 8 && hex.Length != 6)
                    throw new ArgumentException("Error hex color string length.");
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
            }
            catch (Exception)
            {
                throw;
            }
            return new Tuple<byte, byte, byte, byte>(a, r, g, b);
        }
    }
}
