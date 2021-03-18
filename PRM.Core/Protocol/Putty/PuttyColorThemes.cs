using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;
using PRM.Core.Model;

namespace PRM.Core.Protocol.Putty
{
    public static class PuttyColorThemes
    {
        public static Dictionary<string, List<PuttyOptionItem>> GetThemes()
        {
            var uri = new Uri("pack://application:,,,/PRM.Core;component/Resources/Theme/puttyThems.json", UriKind.Absolute);
            var s = Application.GetResourceStream(uri).Stream;
            byte[] bytes = new byte[s.Length];
            s.Read(bytes, 0, (int)s.Length);
            var json = Encoding.UTF8.GetString(bytes);
            var themes = JsonConvert.DeserializeObject<Dictionary<string, List<PuttyOptionItem>>>(json);
            return themes;
        }
    }
}