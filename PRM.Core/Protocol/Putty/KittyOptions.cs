using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace PRM.Core.Protocol.Putty
{
    public class KittyOptions
    {
        public readonly List<PuttyOptionItem> Options = new List<PuttyOptionItem>();
        public readonly string SessionName;
        public KittyOptions(string sessionName)
        {
            SessionName = sessionName;
        }


        private static PuttyOptionItem StringLine2PuttyOptionItem(string line)
        {
            line = line.Trim();
            int a = line.IndexOf(@"\", StringComparison.Ordinal);
            int b = line.LastIndexOf(@"\", StringComparison.Ordinal);
            if (!line.EndsWith(@"\")
                || a == b)
                return null;
            var first = line.Substring(a);
            var second = line.Substring(a + 1, b - a);
            if (string.IsNullOrWhiteSpace(first))
                return null;

            if (int.TryParse(second, out int val))
            {
                return PuttyOptionItem.Create(first, val);
            }
            return PuttyOptionItem.Create(first, second);
        }
        public void ReadFromKittyOptionFile(string filePath)
        {
            var strings = File.ReadAllLines(filePath);
            foreach (var s in strings)
            {
                var item = StringLine2PuttyOptionItem(s);
                if (item == null) continue;
                var obj = Options.FirstOrDefault(x => x.Key == item.Key);
                if (obj != null)
                {
                    obj.Key = item.Key;
                    obj.ValueKind = item.ValueKind;
                    obj.Value = item.Value;
                }
                else
                {
                    Options.Add(item);
                }
            }
        }

        public void ReadFromKittyRegistryTable(string regSessionName)
        {
            string regPath = $"Software\\9bis.com\\KiTTY\\Sessions\\{SessionName}";
            using var regKey = Registry.CurrentUser.CreateSubKey(regPath, RegistryKeyPermissionCheck.ReadSubTree);
            if (regKey == null) return;
            var keys = regKey.GetSubKeyNames();
            foreach (var key in keys)
            {
                var value = regKey.GetValue(key);
            }
        }
    }
}
