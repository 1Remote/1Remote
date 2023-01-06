using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Threading.Tasks;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Newtonsoft.Json.Linq;
using Shawn.Utils;
using Shawn.Utils.Wpf.Image;
using Shawn.Utils.Wpf.Native;

namespace _1RM.Resources.Icons
{
    public class ServerIcons
    {
        #region singleton

        private static ServerIcons? _uniqueInstance;
        public static ServerIcons Instance
        {
            get
            {
                Debug.Assert(_uniqueInstance != null);
                return _uniqueInstance;
            }
        }

        public static void Init()
        {
            _uniqueInstance = new ServerIcons();
        }

        #endregion singleton

        private ServerIcons()
        {
            Task.Factory.StartNew(() =>
            {
                var resourceName = this.GetType().Assembly.GetName().Name + ".g";
                var mgr = new ResourceManager(resourceName, this.GetType().Assembly);
                using var set = mgr.GetResourceSet(CultureInfo.CurrentCulture, true, true);
                if (set == null) return;

                var keys = new List<string>();
                foreach (DictionaryEntry each in set)
                {
                    var key = each.Key.ToString()!;
                    if (key.ToLower().IndexOf("resources/icons/".ToLower(), StringComparison.Ordinal) >= 0
                        && ".png" == Path.GetExtension(key).ToLower())
                    {
                        keys.Add(key);
                    }
                }
                var keyArray = keys.ToArray();
                Array.Sort(keyArray, NaturalCmpLogicalW.Get());
                foreach (var key in keyArray)
                {
                    var s = (UnmanagedMemoryStream)set.GetObject(key, true)!;
                    var img = BitmapFrame.Create(s, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                    var bm = img.ToBitmap();
                    IconsBase64.Add(bm.ToBitmapSource().ToBase64());
                }
            });
        }

        public List<string> IconsBase64 { get; set; } = new List<string>();

        public List<BitmapSource> Icons => new List<BitmapSource>(IconsBase64.Select(x => Convert.FromBase64String(x).BitmapFromBytes().ToBitmapSource()));
    }
}