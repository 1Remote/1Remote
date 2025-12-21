using System;
using System.Diagnostics;
using System.IO;
using Shawn.Utils;
using Shawn.Utils.Wpf;

namespace _1RM.Utils.PuTTY.Model
{
    internal class Utils
    {
        /// <summary>
        /// get resource path from current assembly, save to file
        /// </summary>
        public static void Install(string resourcePath, string installPath)
        {
            var fi = new FileInfo(installPath);
            try
            {
                if (fi?.Directory?.Exists == false)
                    fi.Directory.Create();
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
                return;
            }

            var stream = System.Windows.Application.GetResourceStream(ResourceUriHelper.GetUriFromCurrentAssembly(resourcePath))?.Stream;
            if (stream == null)
            {
                throw new FileNotFoundException($"Resource not found: {resourcePath}");
            }
            if (File.Exists(installPath))
            {
                // verify MD5
                var md5 = MD5Helper.GetMd5Hash32BitString(File.ReadAllBytes(installPath));
                byte[] bytes = new byte[stream.Length];
                stream.Read(bytes, 0, bytes.Length);
                var md5_2 = MD5Helper.GetMd5Hash32BitString(bytes);
                if (md5_2 != md5)
                {
                    foreach (var process in Process.GetProcessesByName(fi!.Name.ToLower().ReplaceLast(".exe", "")))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                    File.Delete(installPath);
                    using var fileStream = File.Create(installPath);
                    stream.Seek(0, SeekOrigin.Begin);
                    stream.CopyTo(fileStream);
                }
            }
            else
            {
                using (var fileStream = File.Create(installPath))
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    stream.CopyTo(fileStream);
                }
                stream.Close();
            }
        }
    }
}
