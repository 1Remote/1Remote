using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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

    #endregion


    public static void SaveTo(this BitmapSource source, string path)
    {
        // TODO 
        throw new NotImplementedException();
    }


    #region Format

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


    public static Bitmap ToBitmap(this BitmapImage bitmapImage)
    {
        return ((BitmapSource)bitmapImage).ToBitmap();
    }
    public static Bitmap ToBitmap<T>(this T source) where T : BitmapSource
    {
        var m = (BitmapSource)source;
        var bmp = new System.Drawing.Bitmap(m.PixelWidth, m.PixelHeight, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        var data = bmp.LockBits(new System.Drawing.Rectangle(System.Drawing.Point.Empty, bmp.Size), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        m.CopyPixels(Int32Rect.Empty, data.Scan0, data.Height * data.Stride, data.Stride);
        bmp.UnlockBits(data);
        return bmp;
    }




    public static BitmapImage ToBitmapImage(this System.Drawing.Bitmap src)
    {
        if (src == null)
            return null;

        using (MemoryStream stream = new MemoryStream())
        {
            // TODO 适配GIF？
            src.Save(stream, ImageFormat.Png); // 坑点：格式选Bmp时，不带透明度
            stream.Position = 0;
            BitmapImage bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            // According to MSDN, "The default OnDemand cache option retains access to the stream until the image is needed."
            // Force the bitmap to load right now so we can dispose the stream.
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = stream;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            return bitmapImage;
        }
    }
    public static BitmapImage ToBitmapImage(this System.Drawing.Image image)
    {
        using (var ms = new MemoryStream())
        {
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
            using (var ms = new MemoryStream())
            {
                encoder.Save(ms);
                ms.Position = 0;

                bitmapImage.BeginInit();
                bitmapImage.StreamSource = ms;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                isCreated = true;
            }
        }
        catch
        {
            isCreated = false;
        }
        return isCreated ? bitmapImage : null;
    }

    #endregion




    public static bool WriteTransformedBitmapToFile<T>(BitmapSource bitmapSource, string fileName) where T : BitmapEncoder, new()
    {
        if (string.IsNullOrEmpty(fileName) || bitmapSource == null)
            return false;

        //creating frame and putting it to Frames collection of selected encoder
        var frame = BitmapFrame.Create(bitmapSource);
        var encoder = new T();
        encoder.Frames.Add(frame);
        try
        {
            using (var fs = new FileStream(fileName, FileMode.Create))
            {
                encoder.Save(fs);
            }
        }
        catch (Exception e)
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
        using (var mStream = new MemoryStream())
        {
            BinaryFormatter bf = new BinaryFormatter();
            bf.Serialize(mStream, image);
            mStream.Seek(0, SeekOrigin.Begin);
            dstBitmap = (Bitmap)bf.Deserialize(mStream);
            mStream.Close();
        }
        return dstBitmap;
    }

    [DllImport("gdi32")]
    private static extern int DeleteObject(IntPtr o); 
    #endregion
}
