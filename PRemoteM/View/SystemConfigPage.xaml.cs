using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using PersonalRemoteManager;
using PRM.Core.DB;
using PRM.Core.Model;
using PRM.ViewModel;
using Shawn.Ulits;
using Binding = System.Windows.Data.Binding;
using UserControl = System.Windows.Controls.UserControl;

namespace PRM.View
{
    /// <summary>
    /// SystemConfigPage.xaml 的交互逻辑
    /// </summary>
    public partial class SystemConfigPage : UserControl
    {
        public VmMain Host;
        public VmSystemConfigPage VmSystemConfigPage;
        public SystemConfigPage(VmMain host)
        {
            Host = host;
            VmSystemConfigPage = new VmSystemConfigPage(host);
            InitializeComponent();
            DataContext = VmSystemConfigPage;

            TextBoxHotKey.Text = GlobalHotkeyHooker.GetHotKeyString(VmSystemConfigPage.QuickConnect.Modifiers, VmSystemConfigPage.QuickConnect.HotKey);
        }

        private void ButtonSelectDb_OnClick(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog();
            dlg.Filter = "Sqlite Database|*.db";
            if (dlg.ShowDialog() == true)
            {
                if (PRM_DAO.TestDb(dlg.FileName, ""))
                {
                    VmSystemConfigPage.General.DbPath = dlg.FileName.Replace(Environment.CurrentDirectory, ".");
                }
                else
                {
                    MessageBox.Show(SystemConfig.GetInstance().Language
                        .GetText("system_options_general_item_database_can_not_open"));
                }
            }
        }


        private uint HotKeyModifiers = 0;
        private Key HotKeyKey = Key.Escape;
        private void TextBox_HotKey_KeyDown(object sender, KeyEventArgs e)
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
            if (key == Key.LeftShift || key == Key.RightShift)
            {
                if ((HotKeyModifiers & (uint)GlobalHotkeyHooker.HotkeyModifiers.Shift) > 0)
                    HotKeyModifiers &= ~(uint)GlobalHotkeyHooker.HotkeyModifiers.Shift;
                else
                    HotKeyModifiers |= (uint)GlobalHotkeyHooker.HotkeyModifiers.Shift;
            }
            if (key == Key.LeftCtrl || key == Key.RightCtrl)
            {
                if ((HotKeyModifiers & (uint)GlobalHotkeyHooker.HotkeyModifiers.Ctrl) > 0)
                    HotKeyModifiers &= ~(uint)GlobalHotkeyHooker.HotkeyModifiers.Ctrl;
                else
                    HotKeyModifiers |= (uint)GlobalHotkeyHooker.HotkeyModifiers.Ctrl;
            }
            if (key == Key.LeftAlt ||key == Key.RightAlt)
            {
                if ((HotKeyModifiers & (uint)GlobalHotkeyHooker.HotkeyModifiers.Alt) > 0)
                    HotKeyModifiers &= ~(uint)GlobalHotkeyHooker.HotkeyModifiers.Alt;
                else
                    HotKeyModifiers |= (uint)GlobalHotkeyHooker.HotkeyModifiers.Alt;
            }
            if (key == Key.LWin || key == Key.RWin)
            {
                if ((HotKeyModifiers & (uint)GlobalHotkeyHooker.HotkeyModifiers.Win) > 0)
                    HotKeyModifiers &= ~(uint)GlobalHotkeyHooker.HotkeyModifiers.Win;
                else
                    HotKeyModifiers |= (uint)GlobalHotkeyHooker.HotkeyModifiers.Win;
            }


            if (HotKeyModifiers > 0)
            {
                if (
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
                    HotKeyKey = key;
                }
            }
            var hotKeyString = GlobalHotkeyHooker.GetHotKeyString(HotKeyModifiers, HotKeyKey);
            TextBoxHotKey.Text = hotKeyString;
        }

        private void TextBox_HotKey_KeyUp(object sender, KeyEventArgs e)
        {
            //var key = e.Key;
            //// In case of an Alt modifier, e.Key returns Key.System and the real key is in e.SystemKey. 
            //switch (key)
            //{
            //    case Key.System:
            //        key = e.SystemKey;
            //        break;
            //    case Key.ImeProcessed:
            //        key = e.ImeProcessedKey;
            //        break;
            //    case Key.DeadCharProcessed:
            //        key = e.DeadCharProcessedKey;
            //        break;
            //}
            //if (key == Key.LeftCtrl ||
            //    key == Key.RightCtrl ||
            //    key == Key.LeftAlt ||
            //    key == Key.RightAlt ||
            //    key == Key.LeftShift ||
            //    key == Key.RightShift ||
            //    key == Key.LWin ||
            //    key == Key.RWin ||
            //    key == Key.Clear ||
            //    key == Key.OemClear ||
            //    key == Key.Escape ||
            //    key == Key.Apps)
            //{
            //    if (key == Key.LeftShift || key == Key.RightShift)
            //        HotKeyModifiers &= ~(uint)GlobalHotkeyHooker.HotkeyModifiers.Shift;
            //    if (key == Key.LeftCtrl || key == Key.RightCtrl)
            //        HotKeyModifiers &= ~(uint)GlobalHotkeyHooker.HotkeyModifiers.Ctrl;
            //    if (key == Key.LeftAlt || key == Key.RightAlt)
            //        HotKeyModifiers &= ~(uint)GlobalHotkeyHooker.HotkeyModifiers.Alt;
            //    if (key == Key.LWin || key == Key.RWin)
            //        HotKeyModifiers &= ~(uint)GlobalHotkeyHooker.HotkeyModifiers.Win;
            //    if (HotKeyModifiers == 0)
            //    {
            //        HotKeyKey = Key.Escape;
            //        HotKeyModifiers = 0;
            //        TextBoxHotKey.Text = GlobalHotkeyHooker.GetHotKeyString(VmSystemConfigPage.QuickConnect.Modifiers, VmSystemConfigPage.QuickConnect.HotKey);
            //    }
            //    return;
            //}


            //if (HotKeyKey == Key.Escape)
            //{
            //    TextBoxHotKey.Text = GlobalHotkeyHooker.GetHotKeyString(VmSystemConfigPage.QuickConnect.Modifiers, VmSystemConfigPage.QuickConnect.HotKey);
            //    HotKeyKey = Key.Escape;
            //    HotKeyModifiers = 0;
            //    return;
            //}



            //{
            //    var hotKeyModifiers = HotKeyModifiers;
            //    var hotKeyKey = HotKeyKey;
            //    HotKeyKey = Key.Escape;
            //    HotKeyModifiers = 0;
            //    if (VmSystemConfigPage.QuickConnect.Modifiers != hotKeyModifiers &&
            //        VmSystemConfigPage.QuickConnect.HotKey != hotKeyKey)
            //    {
            //        // check if HOTKEY_ALREADY_REGISTERED
            //        var r = GlobalHotkeyHooker.GetInstance().Regist(null, hotKeyModifiers, hotKeyKey, () => { });
            //        switch (r.Item1)
            //        {
            //            case GlobalHotkeyHooker.RetCode.Success:
            //                GlobalHotkeyHooker.GetInstance().Unregist(r.Item3);
            //                VmSystemConfigPage.QuickConnect.Modifiers = hotKeyModifiers;
            //                VmSystemConfigPage.QuickConnect.HotKey = hotKeyKey;
            //                return;
            //            case GlobalHotkeyHooker.RetCode.ERROR_HOTKEY_NOT_REGISTERED:
            //                MessageBox.Show(SystemConfig.GetInstance().Language.GetText("info_hotkey_registered_fail") + ": " + r.Item2);
            //                TextBoxHotKey.Text = GlobalHotkeyHooker.GetHotKeyString(VmSystemConfigPage.QuickConnect.Modifiers, VmSystemConfigPage.QuickConnect.HotKey);
            //                break;
            //            case GlobalHotkeyHooker.RetCode.ERROR_HOTKEY_ALREADY_REGISTERED:
            //                MessageBox.Show(SystemConfig.GetInstance().Language.GetText("info_hotkey_already_registered") + ": " + r.Item2);
            //                TextBoxHotKey.Text = GlobalHotkeyHooker.GetHotKeyString(VmSystemConfigPage.QuickConnect.Modifiers, VmSystemConfigPage.QuickConnect.HotKey);
            //                break;
            //            default:
            //                throw new ArgumentOutOfRangeException();
            //        }
            //    }
            //}
        }
    }


    public class EnumServerOrderBy2IsCheckedConverter : IValueConverter
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
}
