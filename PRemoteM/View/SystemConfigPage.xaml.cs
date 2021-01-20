using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using PRM.Core.Model;
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
        public SystemConfigPage(VmMain host, Type t = null)
        {
            Host = host;
            VmSystemConfigPage = new VmSystemConfigPage(host);
            InitializeComponent();
            DataContext = VmSystemConfigPage;


            if (t == typeof(SystemConfigGeneral))
                TabItemGeneral.IsSelected = true;
            if (t == typeof(SystemConfigLanguage))
                TabItemGeneral.IsSelected = true;
            if (t == typeof(SystemConfigQuickConnect))
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
            switch (key)
            {
                case Key.System:
                    key = e.SystemKey;
                    break;
                case Key.ImeProcessed:
                    key = e.ImeProcessedKey;
                    break;
                case Key.DeadCharProcessed:
                    key = e.DeadCharProcessedKey;
                    break;
            }
            if (
                key == Key.Tab ||
                key == Key.CapsLock ||
                key == Key.PrintScreen ||
                key == Key.Scroll ||
                key == Key.Sleep ||
                key == Key.Pause ||
                key == Key.LeftCtrl ||
                key == Key.RightCtrl ||
                key == Key.LeftAlt ||
                key == Key.RightAlt ||
                key == Key.LeftShift ||
                key == Key.RightShift ||
                key == Key.LWin ||
                key == Key.RWin ||
                key == Key.Clear ||
                key == Key.OemClear ||
                key == Key.Escape ||
                key == Key.Apps)
            {
            }
            else
            {
                if (this.IsLoaded)
                {
                    SetHotkeyIsRegistered(VmSystemConfigPage.SystemConfig.QuickConnect.HotKeyModifiers, key);
                }
            }
        }

        private void Modifiers_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.IsLoaded)
            {
                SetHotkeyIsRegistered(VmSystemConfigPage.SystemConfig.QuickConnect.HotKeyModifiers, VmSystemConfigPage.SystemConfig.QuickConnect.HotKeyKey);
            }
        }

        private bool SetHotkeyIsRegistered(HotkeyModifierKeys modifier, Key key)
        {
            if (modifier == SystemConfig.Instance.QuickConnect.HotKeyModifiers
                && key == SystemConfig.Instance.QuickConnect.HotKeyKey)
            {
                VmSystemConfigPage.SystemConfig.QuickConnect.HotKeyModifiers = modifier;
                VmSystemConfigPage.SystemConfig.QuickConnect.HotKeyKey = key;
                return false;
            }


            // check if HOTKEY_ALREADY_REGISTERED
            var r = GlobalHotkeyHooker.Instance.Regist(null, (uint)modifier, key, () => { });
            switch (r.Item1)
            {
                case GlobalHotkeyHooker.RetCode.Success:
                    GlobalHotkeyHooker.Instance.Unregist(r.Item3);
                    VmSystemConfigPage.SystemConfig.QuickConnect.HotKeyModifiers = modifier;
                    VmSystemConfigPage.SystemConfig.QuickConnect.HotKeyKey = key;
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
            VmSystemConfigPage.SystemConfig.QuickConnect.HotKeyModifiers = SystemConfig.Instance.QuickConnect.HotKeyModifiers;
            VmSystemConfigPage.SystemConfig.QuickConnect.HotKeyKey = SystemConfig.Instance.QuickConnect.HotKeyKey;

            return false;
        }

        private void ContentElement_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var dlg = new OpenFileDialog();
            dlg.Filter = "json|*.json";
            dlg.Title = "Select a language json file for translation test.";
            dlg.CheckFileExists = false;
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var path = dlg.FileName;
                    var fi = new FileInfo(path);
                    if (fi.Exists)
                    {
                        var resourceDictionary = MultiLangHelper.LangDictFromJsonFile(fi.FullName);
                        if (resourceDictionary != null)
                        {
                            if (resourceDictionary.Contains("language_name"))
                            {
                                var code = fi.Name.ReplaceLast(fi.Extension, "");
                                SystemConfig.Instance.Language.AddOrUpdateLanguage(code, resourceDictionary["language_name"].ToString(), fi.FullName);
                            }
                            else
                            {
                                MessageBox.Show("json must contain field: \"language_name\"!", SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                            }
                        }
                    }
                }
                catch (Exception ee)
                {
                    SimpleLogHelper.Warning(ee);
                    MessageBox.Show(ee.Message, SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                }
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
        #endregion
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
        #endregion
    }
}
