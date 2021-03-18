using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using PRM.Core.Model;
using Shawn.Utils;
using PRM.ViewModel;

using Shawn.Utils;

using Binding = System.Windows.Data.Binding;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using UserControl = System.Windows.Controls.UserControl;

namespace PRM.View
{
    public partial class SystemConfigPage : UserControl
    {
        public VmMain Host;
        public VmSystemConfigPage VmSystemConfigPage;

        public SystemConfigPage(VmMain host, PrmContext context, Type t = null)
        {
            Host = host;
            VmSystemConfigPage = new VmSystemConfigPage(host, context);
            InitializeComponent();
            DataContext = VmSystemConfigPage;

            if (t == typeof(SystemConfigGeneral)
            || t == typeof(SystemConfigLanguage))
                TabItemGeneral.IsSelected = true;
            if (t == typeof(SystemConfigLauncher))
                TabItemQuick.IsSelected = true;
            if (t == typeof(SystemConfigDataSecurity))
                TabItemDataBase.IsSelected = true;
            if (t == typeof(SystemConfigTheme))
                TabItemTheme.IsSelected = true;
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

            var specialKeys = new[] { Key.Tab ,
                                      Key.CapsLock ,
                                      Key.PrintScreen ,
                                      Key.Scroll ,
                                      Key.Sleep ,
                                      Key.Pause ,
                                      Key.LeftCtrl ,
                                      Key.RightCtrl ,
                                      Key.LeftAlt ,
                                      Key.RightAlt ,
                                      Key.LeftShift ,
                                      Key.RightShift ,
                                      Key.LWin ,
                                      Key.RWin ,
                                      Key.Clear ,
                                      Key.OemClear ,
                                      Key.Escape ,
                                      Key.Apps };

            if (!specialKeys.Contains(key)
            && this.IsLoaded)
            {
                SetHotkeyIsRegistered(VmSystemConfigPage.SystemConfig.Launcher.HotKeyModifiers, key);
            }
        }

        private void Modifiers_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.IsLoaded)
            {
                SetHotkeyIsRegistered(VmSystemConfigPage.SystemConfig.Launcher.HotKeyModifiers, VmSystemConfigPage.SystemConfig.Launcher.HotKeyKey);
            }
        }

        private bool SetHotkeyIsRegistered(HotkeyModifierKeys modifier, Key key)
        {
            if (modifier == SystemConfig.Instance.Launcher.HotKeyModifiers
                && key == SystemConfig.Instance.Launcher.HotKeyKey)
            {
                VmSystemConfigPage.SystemConfig.Launcher.HotKeyModifiers = modifier;
                VmSystemConfigPage.SystemConfig.Launcher.HotKeyKey = key;
                return false;
            }

            // check if HOTKEY_ALREADY_REGISTERED
            var r = GlobalHotkeyHooker.Instance.Register(null, (uint)modifier, key, () => { });
            switch (r.Item1)
            {
                case GlobalHotkeyHooker.RetCode.Success:
                    GlobalHotkeyHooker.Instance.Unregist(r.Item3);
                    VmSystemConfigPage.SystemConfig.Launcher.HotKeyModifiers = modifier;
                    VmSystemConfigPage.SystemConfig.Launcher.HotKeyKey = key;
                    return true;

                case GlobalHotkeyHooker.RetCode.ERROR_HOTKEY_NOT_REGISTERED:
                    MessageBox.Show(SystemConfig.Instance.Language.GetText("hotkey_registered_fail") + ": " + r.Item2, SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                    break;

                case GlobalHotkeyHooker.RetCode.ERROR_HOTKEY_ALREADY_REGISTERED:
                    MessageBox.Show(SystemConfig.Instance.Language.GetText("hotkey_already_registered") + ": " + r.Item2, SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            // HotKey will be auto registered in SearchBox.xaml.cs
            VmSystemConfigPage.SystemConfig.Launcher.HotKeyModifiers = SystemConfig.Instance.Launcher.HotKeyModifiers;
            VmSystemConfigPage.SystemConfig.Launcher.HotKeyKey = SystemConfig.Instance.Launcher.HotKeyKey;

            return false;
        }

        private void ContentElement_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var dlg = new OpenFileDialog();
            dlg.Filter = "json|*.json";
            dlg.Title = "Select a language json file for translation test.";
            dlg.CheckFileExists = false;
            if (dlg.ShowDialog() != true) return;

            var path = dlg.FileName;
            var fi = new FileInfo(path);
            var resourceDictionary = MultiLangHelper.LangDictFromJsonFile(fi.FullName);
            if (resourceDictionary?.Contains("language_name") == true)
            {
                var code = fi.Name.ReplaceLast(fi.Extension, "");
                SystemConfig.Instance.Language.AddJsonLanguageResources(code, fi.FullName);
            }
            else
            {
                MessageBox.Show("json must contain field: \"language_name\"!", SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
            }
        }
    }

    public class ObjEqualParam2Bool : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value.Equals(parameter);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return ((bool)value) ? parameter : Binding.DoNothing;
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
            Key k = (Key)value;
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