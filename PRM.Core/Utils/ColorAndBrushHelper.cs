using System;

namespace Shawn.Utils
{
    public static class ColorAndBrushHelper
    {
        /// <summary>
        /// color in hex string to (a,r,g,b);
        /// #FFFEFDFC   ->  Tuple(255,254,253,252),
        /// #FEFDFC     ->  Tuple(255,254,253,252)
        /// </summary>
        /// <param name="hexColor"></param>
        /// <returns></returns>
        public static Tuple<byte, byte, byte, byte> HexColorToArgb(string hexColor)
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
            }
            return new Tuple<byte, byte, byte, byte>(a, r, g, b);
        }

        /// <summary>
        /// color in (a,r,g,b) to hex string(len = 8);
        /// (255,254,253,252) -> #FFFEFDFC,
        /// </summary>
        /// <param name="hexColor"></param>
        /// <returns></returns>
        public static string ArgbToHexColor(byte a, byte r, byte g, byte b)
        {
            var arr = new[] { a, r, g, b };
            string hex = BitConverter.ToString(arr).Replace("-", string.Empty).ToUpper();
            return $"#{hex}";
        }

        /// <summary>
        /// color in (a,r,g,b) to hex string(len = 6);
        /// (255,254,253,252) -> #FFFEFDFC,
        /// </summary>
        /// <param name="hexColor"></param>
        /// <returns></returns>
        public static string ArgbToHexColor(byte r, byte g, byte b)
        {
            var arr = new[] { r, g, b };
            string hex = BitConverter.ToString(arr).Replace("-", string.Empty).ToUpper();
            return $"#{hex}";
        }

        public static System.Windows.Media.Color HexColorToMediaColor(string hexColor)
        {
            var (a, r, g, b) = HexColorToArgb(hexColor);
            return System.Windows.Media.Color.FromArgb(a, r, g, b);
        }

        public static System.Drawing.Color HexColorToDrawingColor(string hexColor)
        {
            var (a, r, g, b) = HexColorToArgb(hexColor);
            return System.Drawing.Color.FromArgb(a, r, g, b);
        }

        public static string ColorToHexColor(this System.Windows.Media.Color color, bool showAlpha = false)
        {
            if (showAlpha)
            {
                return ArgbToHexColor(color.A, color.R, color.G, color.B);
            }
            else
            {
                return ArgbToHexColor(color.R, color.G, color.B);
            }
        }

        public static string ColorToHexColor(this System.Drawing.Color color, bool showAlpha = false)
        {
            if (showAlpha)
            {
                return ArgbToHexColor(color.A, color.R, color.G, color.B);
            }
            else
            {
                return ArgbToHexColor(color.R, color.G, color.B);
            }
        }

        public static System.Drawing.Brush ColorToDrawingBrush(this System.Drawing.Color color)
        {
            var b = new System.Drawing.SolidBrush(color);
            return b;
        }

        public static System.Drawing.Brush ColorToDrawingBrush(this System.Windows.Media.Color color)
        {
            var b = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B));
            return b;
        }

        public static System.Drawing.Brush ColorToDrawingBrush(string hexColor)
        {
            var color = HexColorToDrawingColor(hexColor);
            var b = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B));
            return b;
        }

        public static System.Windows.Media.Brush ColorToMediaBrush(this System.Drawing.Color color)
        {
            var b = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B));
            return b;
        }

        public static System.Windows.Media.Brush ColorToMediaBrush(this System.Windows.Media.Color color)
        {
            var b = new System.Windows.Media.SolidColorBrush(color);
            return b;
        }

        public static System.Windows.Media.Brush ColorToMediaBrush(string hexColor)
        {
            var color = HexColorToMediaColor(hexColor);
            var b = new System.Windows.Media.SolidColorBrush(color);
            return b;
        }
    }
}