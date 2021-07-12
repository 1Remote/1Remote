using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows;
using Newtonsoft.Json;

namespace PRM.Core.Protocol.Putty
{
    public static class PuttyColorThemes
    {
        public static Dictionary<string, List<PuttyOptionItem>> GetThemes()
        {
            var uri = new Uri("pack://application:,,,/PRM.Core;component/Resources/Theme/puttyThems.json", UriKind.Absolute);
            var s = Application.GetResourceStream(uri)?.Stream;
            Debug.Assert(s != null);

            var bytes = new byte[s.Length];
            s.Read(bytes, 0, (int)s.Length);
            var json = Encoding.UTF8.GetString(bytes);
            var themes = JsonConvert.DeserializeObject<Dictionary<string, List<PuttyOptionItem>>>(json);
            return themes;
        }
    }
}