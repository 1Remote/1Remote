using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace PRM.Core.Protocol.Putty
{
    public static class KittyPortableSessionConfigReader
    {
        public static List<PuttyOptionItem> Read(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }

            var lines = File.ReadAllLines(path);
            var ret = new List<PuttyOptionItem>(lines.Length);
            foreach (var s in lines)
            {
                var line = s.Trim('\t', ' ');
                var i0 = line.IndexOf(@"\", StringComparison.Ordinal);
                if (line.EndsWith(@"\", StringComparison.Ordinal))
                {
                    var para = line.Substring(0, i0);
                    var val = line.Substring(i0 + 1).TrimEnd('\\');
                    if (double.TryParse(val.Replace(',', '_'), out var v))
                    {
                        ret.Add(new PuttyOptionItem() { Key = para, Value = val, ValueKind = RegistryValueKind.DWord });
                    }
                    else
                    {
                        ret.Add(new PuttyOptionItem() { Key = para, Value = val, ValueKind = RegistryValueKind.String });
                    }
                }
            }
            return ret;
        }
    }
}
