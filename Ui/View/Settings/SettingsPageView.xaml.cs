using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using PRM.Model;
using PRM.Service;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.FileSystem;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using UserControl = System.Windows.Controls.UserControl;

namespace PRM.View.Settings
{
    public partial class SettingsPageView : UserControl
    {
        public SettingsPageView()
        {
            InitializeComponent();
            // TODO 跳转目的地
            //if (destination == "Data")
            //    TabItemDataBase.IsSelected = true;
        }

        private void TextBoxKey_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            var key = e.Key;
            // In case of an Alt modifier, e.Key returns Key.System and the real key is in e.SystemKey.
            key = key switch
            {
                Key.System => e.SystemKey,
                Key.ImeProcessed => e.ImeProcessedKey,
                Key.DeadCharProcessed => e.DeadCharProcessedKey,
                _ => key
            };

            var specialKeys = new[] { Key.Tab,
                                      Key.CapsLock,
                                      Key.PrintScreen,
                                      Key.Scroll,
                                      Key.Sleep,
                                      Key.Pause,
                                      Key.LeftCtrl,
                                      Key.RightCtrl,
                                      Key.LeftAlt,
                                      Key.RightAlt,
                                      Key.LeftShift,
                                      Key.RightShift,
                                      Key.LWin,
                                      Key.RWin,
                                      Key.Clear,
                                      Key.OemClear,
                                      Key.Escape,
                                      Key.Apps };

            if (!specialKeys.Contains(key)
            && this.IsLoaded
            && this.DataContext is SettingsPageViewModel vm)
            {
                vm.LauncherHotKeyKey = key;
            }
        }

        private void ContentElement_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var path = SelectFileHelper.OpenFile(title: "Select a language resource file for translation test.",
                filter: "xaml|*.xaml");
            if (path == null) return;
            var fi = new FileInfo(path);
            var resourceDictionary = MultiLanguageHelper.LangDictFromXamlFile(fi.FullName);
            if (resourceDictionary?.Contains("language_name") != true)
            {
                MessageBox.Show("language resource must contain field: \"language_name\"!", IoC.Get<ILanguageService>().Translate("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                return;
            }

            var en = IoC.Get<LanguageService>().Resources["en-us"];
            Debug.Assert(en != null);
            var missingFields = MultiLanguageHelper.FindMissingFields(en, resourceDictionary);
            if (missingFields.Count > 0)
            {
                var mf = string.Join(", ", missingFields);
                MessageBox.Show($"language resource missing:\r\n {mf}", IoC.Get<ILanguageService>().Translate("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                return;
            }

            var code = fi.Name.ReplaceLast(fi.Extension, "");
            IoC.Get<ILanguageService>().AddXamlLanguageResources(code, fi.FullName);
        }
    }

    /// <summary>
    /// key board key A -> string "A"
    /// </summary>
    public class Key2KeyStringConverter : IValueConverter
    {
        #region IValueConverter 成员

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var k = (Key)value;
            return k.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion IValueConverter 成员
    }

    public class StringIsEmpty2BoolConverter : IValueConverter
    {
        #region IValueConverter 成员

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            string str = value?.ToString();
            return string.IsNullOrEmpty(str);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion IValueConverter 成员
    }
}