using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows;
using Newtonsoft.Json;
using PRM.Core.Protocol.Putty;

namespace PRM.Core.External.KiTTY
{
    public static class PuttyThemes
    {
        public static Dictionary<string, List<KittyConfigKeyValuePair>> GetThemes()
        {
            var uri = new Uri("pack://application:,,,/PRM.Core;component/External/KiTTY/PuttyThemes.json", UriKind.Absolute);
            var s = Application.GetResourceStream(uri)?.Stream;
            Debug.Assert(s != null);

            var bytes = new byte[s.Length];
            s.Read(bytes, 0, (int)s.Length);
            var json = Encoding.UTF8.GetString(bytes);
            var themes = JsonConvert.DeserializeObject<Dictionary<string, List<KittyConfigKeyValuePair>>>(json);
            return themes;
        }
    }
}