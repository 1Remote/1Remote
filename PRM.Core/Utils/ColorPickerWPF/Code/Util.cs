using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using System.Xml;
using System.Xml.Serialization;
using Color = System.Windows.Media.Color;

namespace ColorPickerWPF.Code
{
    internal static class Util
    {
        [DllImport("shlwapi.dll")]
        public static extern int ColorHLSToRGB(int H, int L, int S);

        public static Color ColorFromHSL(int H, int S, int L)
        {
            int colorInt = ColorHLSToRGB(H, L, S);
            byte[] bytes = BitConverter.GetBytes(colorInt);
            return Color.FromArgb(255, bytes[0], bytes[1], bytes[2]);
        }

        
        public static string ToHexString(this Color c)
        {
            return "#" + c.A.ToString("X2") + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
        }

        public static Color ColorFromHexString(string hex)
        {
            return Color.FromRgb(
               Convert.ToByte(hex.Substring(1, 2), 16),
               Convert.ToByte(hex.Substring(3, 2), 16),
               Convert.ToByte(hex.Substring(5, 2), 16));
        }

        public static BitmapImage GetBitmapImage(BitmapSource bitmapSource)
        {
            JpegBitmapEncoder encoder = new JpegBitmapEncoder();
            MemoryStream memoryStream = new MemoryStream();
            BitmapImage bImg = new BitmapImage();

            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
            encoder.Save(memoryStream);

            memoryStream.Position = 0;
            bImg.BeginInit();
            bImg.StreamSource = memoryStream;
            bImg.EndInit();

            memoryStream.Close();

            return bImg;
        }
        
        public static float GetHue(this System.Windows.Media.Color c)
        {
            var color = System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
            return color.GetHue();
        }

        public static float GetBrightness(this System.Windows.Media.Color c)
        {
            var color = System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
            return color.GetBrightness();
        }

        public static float GetSaturation(this System.Windows.Media.Color c)
        {
            var color = System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
            return color.GetSaturation();
        }

        public static Color FromAhsb(int alpha, float hue, float saturation, float brightness)
        {

            if (0 > alpha || 255 < alpha)
            {
                throw new ArgumentOutOfRangeException("alpha", alpha,
                  "Value must be within a range of 0 - 255.");
            }
            if (0f > hue || 360f < hue)
            {
                throw new ArgumentOutOfRangeException("hue", hue,
                  "Value must be within a range of 0 - 360.");
            }
            if (0f > saturation || 1f < saturation)
            {
                throw new ArgumentOutOfRangeException("saturation", saturation,
                  "Value must be within a range of 0 - 1.");
            }
            if (0f > brightness || 1f < brightness)
            {
                throw new ArgumentOutOfRangeException("brightness", brightness,
                  "Value must be within a range of 0 - 1.");
            }

            if (0 == saturation)
            {
                return Color.FromArgb((byte)alpha, Convert.ToByte(brightness * 255),
                  Convert.ToByte(brightness * 255), Convert.ToByte(brightness * 255));
            }

            float fMax, fMid, fMin;
            int iSextant;
            byte iMax, iMid, iMin;

            if (0.5 < brightness)
            {
                fMax = brightness - (brightness * saturation) + saturation;
                fMin = brightness + (brightness * saturation) - saturation;
            }
            else
            {
                fMax = brightness + (brightness * saturation);
                fMin = brightness - (brightness * saturation);
            }

            iSextant = (int)Math.Floor(hue / 60f);
            if (300f <= hue)
            {
                hue -= 360f;
            }
            hue /= 60f;
            hue -= 2f * (float)Math.Floor(((iSextant + 1f) % 6f) / 2f);
            if (0 == iSextant % 2)
            {
                fMid = hue * (fMax - fMin) + fMin;
            }
            else
            {
                fMid = fMin - hue * (fMax - fMin);
            }

            iMax = Convert.ToByte(fMax * 255);
            iMid = Convert.ToByte(fMid * 255);
            iMin = Convert.ToByte(fMin * 255);

            switch (iSextant)
            {
                case 1:
                    return Color.FromArgb((byte)alpha, iMid, iMax, iMin);
                case 2:
                    return Color.FromArgb((byte)alpha, iMin, iMax, iMid);
                case 3:
                    return Color.FromArgb((byte)alpha, iMin, iMid, iMax);
                case 4:
                    return Color.FromArgb((byte)alpha, iMid, iMin, iMax);
                case 5:
                    return Color.FromArgb((byte)alpha, iMax, iMin, iMid);
                default:
                    return Color.FromArgb((byte)alpha, iMax, iMid, iMin);
            }
        }

        // https://stackoverflow.com/questions/5091455/web-color-list-in-c-sharp-application
        public static List<Color> GetWebColors()
        {
            Type colors = typeof(System.Drawing.Color);
            PropertyInfo[] colorInfo = colors.GetProperties(BindingFlags.Public |
                BindingFlags.Static);
            List<Color> list = new List<Color>();
            foreach (PropertyInfo info in colorInfo)
            {
                var c = System.Drawing.Color.FromName(info.Name);
                list.Add(Color.FromArgb(c.A, c.R, c.G, c.B));
            }

            
            return list;
        }


        public static void SaveToXml<T>(this T obj, string filename)
        {
            var xml = obj.GetXmlText();

            File.WriteAllText(filename, xml);
        }

        public static string GetXmlText<T>(this T obj)
        {
            var xmlSerializer = new XmlSerializer(typeof(T));

            var sww = new StringWriter();

            var settings = new XmlWriterSettings()
            {
                Indent = true,
                IndentChars = "    ",
                NewLineOnAttributes = false,
                //OmitXmlDeclaration = true
            };
            var writer = XmlWriter.Create(sww, settings);

            xmlSerializer.Serialize(writer, obj);
            var xml = sww.ToString();

            writer.Close();
            writer.Dispose();


            return xml;
        }

        public static T LoadFromXml<T>(this T obj, string filename)
        {
            T result = default(T);
            if (File.Exists(filename))
            {
                var sr = new StreamReader(filename);
                var xr = new XmlTextReader(sr);

                var xmlSerializer = new XmlSerializer(typeof(T));
                
                result = (T)xmlSerializer.Deserialize(xr);

                xr.Close();
                sr.Close();
                xr.Dispose();
                sr.Dispose();
            }
            return result;
        }

        public static T LoadFromXmlText<T>(this T obj, string xml)
        {
            T result = default(T);
            if (!String.IsNullOrEmpty(xml))
            {
                var xr = XmlReader.Create(new StringReader(xml));

                var xmlSerializer = new XmlSerializer(typeof(T));
                
                result = (T)xmlSerializer.Deserialize(xr);

                xr.Close();
                xr.Dispose();
            }
            return result;
        }

    }
}
