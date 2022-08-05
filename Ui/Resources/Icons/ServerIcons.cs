using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Shawn.Utils;
using Shawn.Utils.Wpf.Image;

namespace _1RM.Resources.Icons
{
    public class ServerIcons
    {
        #region singleton

        private static ServerIcons? _uniqueInstance;
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
            var resourceName = this.GetType().Assembly.GetName().Name + ".g";
            var mgr = new ResourceManager(resourceName, this.GetType().Assembly);
            using var set = mgr.GetResourceSet(CultureInfo.CurrentCulture, true, true);
            if (set == null) return;
            foreach (DictionaryEntry each in set)
            {
                var key = each.Key.ToString()!;
                if (key.ToLower().IndexOf("resources/icons/".ToLower(), StringComparison.Ordinal) >= 0
                    && ".png" == Path.GetExtension(key).ToLower())
                {
                    var s = (UnmanagedMemoryStream)set.GetObject(key, true)!;
                    var img = BitmapFrame.Create(s, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                    var bm = img.ToBitmap();
                    Icons.Add(bm.ToBitmapSource());
                }
            }
        }

        public List<BitmapSource> Icons { get; set; } = new List<BitmapSource>();
    }
}