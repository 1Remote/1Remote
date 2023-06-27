using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace _1RM.Utils
{
    internal class IcoHelper
    {
        public static bool ConvertToIcon(Stream input, Stream output, int size = 16, bool preserveAspectRatio = false)
        {
            if (Image.FromStream(input) is Bitmap inputBitmap)
            {
                return ConvertToIcon(inputBitmap, output, size, preserveAspectRatio);
            }
            return false;
        }

        /// <summary>
        /// Converts a PNG image to a icon (ico)
        /// </summary>
        /// <param name="inputBitmap"></param>
        /// <param name="output">The output stream</param>
        /// <param name="size">The size (16x16 px by default)</param>
        /// <param name="preserveAspectRatio">Preserve the aspect ratio</param>
        /// <returns>Whether or not the icon was successfully generated</returns>
        public static bool ConvertToIcon(Bitmap inputBitmap, Stream output, int size = 16, bool preserveAspectRatio = true)
        {
            int width, height;
            if (preserveAspectRatio)
            {
                width = size;
                height = (int)(inputBitmap.Height * 1.0 / inputBitmap.Width * size);
            }
            else
            {
                width = height = size;
            }
            var newBitmap = new Bitmap(inputBitmap, new Size(width, height));
            // save the resized png into a memory stream for future use
            using var memoryStream = new MemoryStream();
            newBitmap.Save(memoryStream, ImageFormat.Png);

            var iconWriter = new BinaryWriter(output);
            // 0-1 reserved, 0
            iconWriter.Write((byte)0);
            iconWriter.Write((byte)0);

            // 2-3 image type, 1 = icon, 2 = cursor
            iconWriter.Write((short)1);

            // 4-5 number of images
            iconWriter.Write((short)1);

            // image entry 1
            // 0 image width
            iconWriter.Write((byte)width);
            // 1 image height
            iconWriter.Write((byte)height);

            // 2 number of colors
            iconWriter.Write((byte)0);

            // 3 reserved
            iconWriter.Write((byte)0);

            // 4-5 color planes
            iconWriter.Write((short)0);

            // 6-7 bits per pixel
            iconWriter.Write((short)32);

            // 8-11 size of image data
            iconWriter.Write((int)memoryStream.Length);

            // 12-15 offset of image data
            iconWriter.Write((int)(6 + 16));

            // write image data
            // png data must contain the whole png data file
            iconWriter.Write(memoryStream.ToArray());

            iconWriter.Flush();

            return true;

        }

        /// <summary>
        /// Converts a PNG image to a icon (ico)
        /// </summary>
        /// <param name="inputPath">The input path</param>
        /// <param name="outputPath">The output path</param>
        /// <param name="size">The size (16x16 px by default)</param>
        /// <param name="preserveAspectRatio">Preserve the aspect ratio</param>
        /// <returns>Whether or not the icon was successfully generated</returns>
        public static bool ConvertToIcon(string inputPath, string outputPath, int size = 16, bool preserveAspectRatio = true)
        {
            using var inputStream = new FileStream(inputPath, FileMode.Open);
            var fi = new FileInfo(outputPath);
            if (fi.Directory?.Exists == false)
            {
                fi.Directory.Create();
            }
            using var outputStream = new FileStream(outputPath, FileMode.OpenOrCreate);
            return ConvertToIcon(inputStream, outputStream, size, preserveAspectRatio);
        }

        public static bool ConvertToIcon(Bitmap inputBitmap, string outputPath, int size = 16, bool preserveAspectRatio = true)
        {
            var fi = new FileInfo(outputPath);
            if (fi.Directory?.Exists == false)
            {
                fi.Directory.Create();
            }
            using var outputStream = new FileStream(outputPath, FileMode.OpenOrCreate);
            return ConvertToIcon(inputBitmap, outputStream, size, preserveAspectRatio);
        }
    }
}
