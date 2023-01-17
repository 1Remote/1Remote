using System;
using System.Collections.Generic;
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

namespace _1RM.View.Settings.Launcher
{
    /// <summary>
    /// LauncherSettingView.xaml 的交互逻辑
    /// </summary>
    public partial class LauncherSettingView : UserControl
    {
        public LauncherSettingView()
        {
            InitializeComponent();
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
                && this.DataContext is LauncherSettingViewModel vm)
            {
                vm.LauncherHotKeyKey = key;
            }
        }
    }
}
