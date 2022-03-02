using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using MSTSCLib;
using PRM.Core.Model;
using Shawn.Utils;
using PRM.ViewModel;
using PRM.ViewModel.Configuration;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.FileSystem;
using Binding = System.Windows.Data.Binding;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using UserControl = System.Windows.Controls.UserControl;

namespace PRM.View
{
    public partial class SystemConfigPage : UserControl
    {
        private readonly ConfigurationViewModel _vm;

        private readonly PrmContext _context;

        public SystemConfigPage(PrmContext context, ConfigurationViewModel vm, string destination = null)
        {
            _context = context;
            InitializeComponent();
            _vm = vm;
            DataContext = _vm;

            if (destination == "Data")
                TabItemDataBase.IsSelected = true;
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
            && this.IsLoaded)
            {
                _vm.LauncherHotKeyKey = key;
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
                MessageBox.Show("language resource must contain field: \"language_name\"!", _context.LanguageService.Translate("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                return;
            }

            var en = _context.LanguageService.Resources["en-us"];
            Debug.Assert(en != null);
            var missingFields = MultiLanguageHelper.FindMissingFields(en, resourceDictionary);
            if (missingFields.Count > 0)
            {
                var mf = string.Join(", ", missingFields);
                MessageBox.Show($"language resource missing:\r\n {mf}", _context.LanguageService.Translate("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                return;
            }

            var code = fi.Name.ReplaceLast(fi.Extension, "");
            _context.LanguageService.AddXamlLanguageResources(code, fi.FullName);
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