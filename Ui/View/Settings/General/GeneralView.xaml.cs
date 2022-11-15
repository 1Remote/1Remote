using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using _1RM.Service;
using _1RM.Utils;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.FileSystem;

namespace _1RM.View.Settings.General
{
    /// <summary>
    /// GeneralSettingView.xaml 的交互逻辑
    /// </summary>
    public partial class GeneralSettingView : UserControl
    {
        public GeneralSettingView()
        {
            InitializeComponent();
        }



        //private void ContentElement_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        //{
        //    var path = SelectFileHelper.OpenFile(title: "Select a language resource file for translation test.",
        //        filter: "xaml|*.xaml");
        //    if (path == null) return;
        //    var fi = new FileInfo(path);
        //    var resourceDictionary = MultiLanguageHelper.LangDictFromXamlFile(fi.FullName);
        //    if (resourceDictionary?.Contains("language_name") != true)
        //    {
        //        MessageBoxHelper.ErrorAlert("language resource must contain field: \"language_name\"!");
        //        return;
        //    }

        //    var en = IoC.Get<LanguageService>().Resources["en-us"];
        //    Debug.Assert(en != null);
        //    var missingFields = MultiLanguageHelper.FindMissingFields(en, resourceDictionary);
        //    if (missingFields.Count > 0)
        //    {
        //        var mf = string.Join(", ", missingFields);
        //        MessageBoxHelper.ErrorAlert($"language resource missing:\r\n {mf}");
        //        return;
        //    }

        //    var code = fi.Name.ReplaceLast(fi.Extension, "");
        //    IoC.Get<ILanguageService>().AddXamlLanguageResources(code, fi.FullName);
        //}
    }
}
