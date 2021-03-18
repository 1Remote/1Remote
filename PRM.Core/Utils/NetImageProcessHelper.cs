using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Shawn.Utils
{
    public static class NetImageProcessHelper
    {
        #region Processing

        public static BitmapSource Resize(this BitmapSource source, double scalingX, double scalingY)
        {
            var transformedBitmap = new TransformedBitmap();
            transformedBitmap.BeginInit();
            transformedBitmap.Source = source;
            transformedBitmap.Transform = new ScaleTransform(scalingX, scalingY);
            transformedBitmap.EndInit();
            return transformedBitmap;
            //var targetBitmap = new TransformedBitmap((source as BitmapImage), new ScaleTransform(scalingX, scalingY));
        }

        public static Image ToThumbnail(this Bitmap image, int width, int height)
        {
            return image.GetThumbnailImage(width, height, null, IntPtr.Zero);

            // SLOW
            //var thumbnail = new Bitmap(width, height, image.PixelFormat);
            //thumbnail.SetResolution(72, 72);
            //using (Graphics g = Graphics.FromImage(thumbnail))
            //{
            //    g.Clear(Color.White);
            //    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            //    g.SmoothingMode = SmoothingMode.HighSpeed;
            //    g.PixelOffsetMode = PixelOffsetMode.HighSpeed;
            //    g.CompositingQuality = CompositingQuality.HighSpeed;
            //    g.DrawImage(image, new Rectangle(0, 0, width, height), new
            //        Rectangle(0, 0, image.Width, image.Height), GraphicsUnit.Pixel);
            //    return thumbnail;
            //}
        }

        public static Bitmap Roi(this Bitmap image, Rectangle section)
        {
            var roiBit = new Bitmap(section.Width, section.Height);
            using (var g = Graphics.FromImage(roiBit))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                g.SmoothingMode = SmoothingMode.HighSpeed;
                g.PixelOffsetMode = PixelOffsetMode.HighSpeed;
                g.DrawImage(image, 0, 0, section, GraphicsUnit.Pixel);
                return roiBit;
            }
        }

        public static Bitmap Roi<T>(this T source, Rectangle section) where T : BitmapSource
        {
            return source.ToBitmap().Roi((section));
        }

        public static Bitmap RoiClone(this Bitmap image, Rectangle section)
        {
            var roiBit = image.Clone(section, image.PixelFormat);
            return roiBit;
        }

        #endregion Processing

        public static void SaveTo(this BitmapSource source, string path)
        {
            using var fileStream = new FileStream(path, FileMode.Create);
            BitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            encoder.Save(fileStream);
        }

        #region Format

        public static Bitmap ToBitmap(this BitmapImage bitmapImage)
        {
            return ((BitmapSource)bitmapImage).ToBitmap();
        }

        public static Bitmap ToBitmap<T>(this T source) where T : BitmapSource
        {
            var m = (BitmapSource)source;
            var bmp = new System.Drawing.Bitmap(m.PixelWidth, m.PixelHeight,
                System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            var data = bmp.LockBits(new System.Drawing.Rectangle(System.Drawing.Point.Empty, bmp.Size),
                System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            m.CopyPixels(Int32Rect.Empty, data.Scan0, data.Height * data.Stride, data.Stride);
            bmp.UnlockBits(data);
            return bmp;
        }

        public static Bitmap ToBitmap(this System.Drawing.Image image)
        {
            using var ms = new MemoryStream();
            image.Save(ms, ImageFormat.Png);
            ms.Seek(0, SeekOrigin.Begin);
            var bitmapImage = new Bitmap(ms);
            return bitmapImage;
        }

        public static Bitmap BitmapFromBytes(byte[] bytes)
        {
            var ms = new MemoryStream(bytes);
            var ret = new Bitmap(ms);
            return ret;
        }

        public static Bitmap BitmapFromBase64(string base64)
        {
            return BitmapFromBytes(Convert.FromBase64String(base64));
        }

        public static byte[] ToBytes(this Image img)
        {
            using var ms = new MemoryStream();
            img.Save(ms, img.RawFormat);
            return ms.ToArray();
        }

        public static byte[] ToBytes(this Bitmap bitmap)
        {
            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            byte[] byteImage = ms.ToArray();
            return byteImage;
        }

        public static byte[] ToBytes<T>(this T source) where T : BitmapSource
        {
            return source?.ToBitmap()?.ToBytes();
        }

        public static string ToBase64(this Image img)
        {
            return Convert.ToBase64String(img.ToBytes());
        }

        public static string ToBase64(this Bitmap bitmap)
        {
            return Convert.ToBase64String(bitmap.ToBytes());
        }

        public static string ToBase64<T>(this T source) where T : BitmapSource
        {
            return Convert.ToBase64String(source.ToBytes());
        }

        public static BitmapImage ToBitmapImage(this System.Drawing.Bitmap src)
        {
            if (src == null)
                return null;

            using var ms = new MemoryStream();
            src.Save(ms, ImageFormat.Png);
            ms.Seek(0, SeekOrigin.Begin);
            BitmapImage bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            // According to MSDN, "The default OnDemand cache option retains access to the stream until the image is needed."
            // Force the bitmap to load right now so we can dispose the stream.
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = ms;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            return bitmapImage;
        }

        public static BitmapImage ToBitmapImage(this System.Drawing.Image image)
        {
            using var ms = new MemoryStream();
            image.Save(ms, ImageFormat.Png);
            ms.Seek(0, SeekOrigin.Begin);

            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = ms;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            return bitmapImage;
        }

        public static BitmapImage ToBitmapImage<T>(this BitmapSource bitmapSource) where T : BitmapEncoder, new()
        {
            var frame = BitmapFrame.Create(bitmapSource);
            var encoder = new T();
            encoder.Frames.Add(frame);
            var bitmapImage = new BitmapImage();
            bool isCreated;
            try
            {
                using var ms = new MemoryStream();
                encoder.Save(ms);
                ms.Position = 0;

                bitmapImage.BeginInit();
                bitmapImage.StreamSource = ms;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                isCreated = true;
            }
            catch
            {
                isCreated = false;
            }

            return isCreated ? bitmapImage : null;
        }

        public static Icon ToIcon(this Image img)
        {
            if (img.Size.Width > 256 || img.Size.Height > 256)
            {
                double k1 = 256.0 / img.Size.Width;
                double k2 = 256.0 / img.Size.Height;
                double k = Math.Max(k1, k2);
                int nw = Math.Min((int)(img.Size.Width * k), 256);
                int nh = Math.Min((int)(img.Size.Height * k), 256);
                img = img.ToBitmap().ToThumbnail(nw, nh);
            }

            var ms = new System.IO.MemoryStream();
            var bw = new System.IO.BinaryWriter(ms);
            // Header
            bw.Write((short)0);   // 0 : reserved
            bw.Write((short)1);   // 2 : 1=ico, 2=cur
            bw.Write((short)1);   // 4 : number of images
            // Image directory
            var w = img.Width;
            if (w >= 256) w = 0;
            bw.Write((byte)w);    // 0 : width of image
            var h = img.Height;
            if (h >= 256) h = 0;
            bw.Write((byte)h);    // 1 : height of image
            bw.Write((byte)0);    // 2 : number of colors in palette
            bw.Write((byte)0);    // 3 : reserved
            bw.Write((short)0);   // 4 : number of color planes
            bw.Write((short)0);   // 6 : bits per pixel
            var sizeHere = ms.Position;
            bw.Write((int)0);     // 8 : image size
            var start = (int)ms.Position + 4;
            bw.Write(start);      // 12: offset of image data
            // Image data
            img.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            var imageSize = (int)ms.Position - start;
            ms.Seek(sizeHere, System.IO.SeekOrigin.Begin);
            bw.Write(imageSize);
            ms.Seek(0, System.IO.SeekOrigin.Begin);

            // And load it
            return new Icon(ms);
        }

        public static Icon ToIcon(this Bitmap bitmap)
        {
            var src = bitmap;
            if (src.Width > 256 || src.Height > 256)
            {
                double k1 = 256.0 / src.Width;
                double k2 = 256.0 / src.Height;
                double k = Math.Max(k1, k2);
                int nw = Math.Min((int)(src.Width * k), 256);
                int nh = Math.Min((int)(src.Height * k), 256);
                src = src.ToThumbnail(nw, nh).ToBitmap();
            }
            return Icon.FromHandle(src.GetHicon());
        }

        public static Icon ToIcon<T>(this T source) where T : BitmapSource
        {
            var src = source.ToBitmap();
            return src.ToIcon();
        }

        // Convert a Bitmap to a BitmapSource.
        public static BitmapSource ToBitmapSource(this System.Drawing.Bitmap src)
        {
            if (src == null)
                return null;
            var ptr = src.GetHbitmap(); //obtain the Hbitmap
            try
            {
                var bs = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    ptr,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                return bs;
            }
            finally
            {
                DeleteObject(ptr); //release the HBitmap
            }
        }

        public static BitmapSource ToBitmapSource(this System.Drawing.Image image)
        {
            return (BitmapSource)image.ToBitmapImage();
        }

        public static Image ImageFromBytes(byte[] byteArrayIn)
        {
            using var mStream = new MemoryStream(byteArrayIn);
            return Image.FromStream(mStream);
        }

        public static Image ToImage(this Bitmap src)
        {
            if (src == null)
                return null;

            using var ms = new MemoryStream();
            src.Save(ms, ImageFormat.Png); // 坑点：格式选Bmp时，不带透明度
            ms.Seek(0, SeekOrigin.Begin);
            return Image.FromStream(ms);
        }

        public static Image ToImage<T>(this BitmapSource bitmapSource) where T : BitmapEncoder, new()
        {
            var frame = BitmapFrame.Create(bitmapSource);
            var encoder = new T();
            encoder.Frames.Add(frame);
            try
            {
                using var ms = new MemoryStream();
                encoder.Save(ms);
                ms.Seek(0, SeekOrigin.Begin);
                return Image.FromStream(ms);
            }
            catch
            {
            }
            return null;
        }

        #endregion Format

        public static Bitmap ReadImgFile(string filePath)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = File.OpenRead(filePath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                return bitmap.ToBitmap();
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static bool WriteTransformedBitmapToFile<T>(BitmapSource bitmapSource, string fileName)
            where T : BitmapEncoder, new()
        {
            if (string.IsNullOrEmpty(fileName) || bitmapSource == null)
                return false;

            //creating frame and putting it to Frames collection of selected encoder
            var frame = BitmapFrame.Create(bitmapSource);
            var encoder = new T();
            encoder.Frames.Add(frame);
            try
            {
                using var fs = new FileStream(fileName, FileMode.Create);
                encoder.Save(fs);
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        #region Common

        public static Bitmap DeepClone(this Bitmap image)
        {
            //return image;
            if (image == null)
                return null;
            Bitmap dstBitmap = null;
            using var mStream = new MemoryStream();
            BinaryFormatter bf = new BinaryFormatter();
            bf.Serialize(mStream, image);
            mStream.Seek(0, SeekOrigin.Begin);
            dstBitmap = (Bitmap)bf.Deserialize(mStream);
            mStream.Close();

            return dstBitmap;
        }

        [DllImport("gdi32")]
        private static extern int DeleteObject(IntPtr o);

        #endregion Common
    }
}