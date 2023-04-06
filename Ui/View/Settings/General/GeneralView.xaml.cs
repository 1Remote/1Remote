using System.Windows.Controls;

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
