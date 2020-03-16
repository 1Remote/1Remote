using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

public static class NetImageFormatHelper
{
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

    // Convert a Bitmap to a BitmapSource. 
    public static BitmapSource ToBitmapSource(this System.Drawing.Bitmap src)
    {
        if (src == null)
            return null;

        IntPtr ptr = src.GetHbitmap(); //obtain the Hbitmap
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

    public static Bitmap ToBitmap(this BitmapImage bitmapImage)
    {
        using(MemoryStream outStream = new MemoryStream())
        {
            BitmapEncoder enc = new BmpBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bitmapImage));
            enc.Save(outStream);
            System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(outStream);
            return new Bitmap(bitmap);
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
}
