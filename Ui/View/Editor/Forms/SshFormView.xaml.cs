using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using _1RM.Model.Protocol;
using _1RM.Utils;
using _1RM.Utils.PuTTY;
using _1RM.Utils.PuTTY.Model;
using _1RM.Utils.PuTTY.Model;
using Shawn.Utils.Wpf.FileSystem;

namespace _1RM.View.Editor.Forms
{
    public partial class SshFormView : UserControl
    {
        public SshFormView() : base()
        {
            InitializeComponent();
            //Loaded += (sender, args) =>
            //{
            //    if (DataContext is SshFormViewModel { New: var ssh })
            //    {
            //        bool? privateKey = false;
            //        if (ssh.PrivateKey == ssh.ServerEditorDifferentOptions)
            //        {
            //            privateKey = null;
            //        }
            //        if (!string.IsNullOrEmpty(ssh.PrivateKey))
            //        {
            //            privateKey = true;
            //        }
            //        CbUsePrivateKey.IsChecked = privateKey;
            //    }
            //};
        }

        private void ButtonOpenPrivateKey_OnClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is SshFormViewModel { New: var ssh })
            {
                var path = SelectFileHelper.OpenFile(filter: "ppk|*.*", currentDirectoryForShowingRelativePath: Environment.CurrentDirectory);
                if (path == null) return;
                ssh.PrivateKey = path;
            }
        }

        private void CbUsePrivateKey_OnChecked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && DataContext is SshFormViewModel { New: var ssh })
                if (cb.IsChecked == false)
                {
                    ssh.PrivateKey = "";
                }
                else
                {
                    ssh.Password = "";
                }
        }

        private void ButtonSelectSessionConfigFile_OnClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is SshFormViewModel vm)
            {
                var path = SelectFileHelper.OpenFile(filter: "KiTTY Session|*.*");
                if (path == null) return;
                if (File.Exists(path) && KittyConfig.Read(path)?.Count > 0)
                {
                    vm.New.ExternalKittySessionConfigPath = path;
                }
                else
                {
                    vm.New.ExternalKittySessionConfigPath = "";
                    MessageBoxHelper.Warning("Invalid KiTTY session config file.");
                }
            }
        }
    }



    public class ConverterESshVersion : IValueConverter
    {
        #region IValueConverter 成员  
        public object Convert(object? value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return 2;
            return ((int)value - 1).ToString();
        }

        public object? ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var i = int.Parse(value?.ToString() ?? "0");
            if (i == 0)
                return 1;
            if (i == 1)
                return 2;
            return null;
        }
        #endregion
    }
}
