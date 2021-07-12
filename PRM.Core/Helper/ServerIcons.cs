using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Windows.Media.Imaging;
using PRM.Core.Model;
using Shawn.Utils;

namespace PRM.Core.Protocol
{
    public class ServerIcons
    {
        #region singleton

        private static ServerIcons _uniqueInstance;
        private static readonly object InstanceLock = new object();

        public static ServerIcons Instance
        {
            get
            {
                lock (InstanceLock)
                {
                    _uniqueInstance ??= new ServerIcons();
                }
                return _uniqueInstance;
            }
        }

        #endregion singleton

        private ServerIcons()
        {
            string iconFolderPath = SystemConfig.Instance.General.IconFolderPath;
            var di = new DirectoryInfo(iconFolderPath);
            if (di.Exists && di.GetFiles().Length > 0)
            {
                var jpgs = di.GetFiles("*.jpg");
                var pngs = di.GetFiles("*.png");
                var bmps = di.GetFiles("*.bmp");
                var icons = di.GetFiles("*.icon");
                var imgs = jpgs.Union(pngs).Union(icons).Union(bmps);
                foreach (var fileInfo in imgs)
                {
                    var bs = NetImageProcessHelper.ReadImgFile(fileInfo.FullName);
                    if (bs != null)
                        Icons.Add(bs.ToBitmapSource());
                }
            }
            else if (!di.Exists)
                di.Create();

            string resourceName = this.GetType().Assembly.GetName().Name + ".g";
            ResourceManager mgr = new ResourceManager(resourceName, this.GetType().Assembly);
            using (ResourceSet set = mgr.GetResourceSet(CultureInfo.CurrentCulture, true, true))
            {
                foreach (DictionaryEntry each in set)
                {
                    if (each.Key.ToString().StartsWith("resources/icons/", true, CultureInfo.CurrentCulture)
                    && ".png" == Path.GetExtension(each.Key.ToString()).ToLower())
                    {
                        var s = (UnmanagedMemoryStream)set.GetObject(each.Key.ToString(), true);
                        var img = BitmapFrame.Create(s, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                        var bm = img.ToBitmap();
                        Icons.Add(bm.ToBitmapSource());
                    }
                }
            }
        }

        public List<BitmapSource> Icons { get; set; } = new List<BitmapSource>();
    }
}