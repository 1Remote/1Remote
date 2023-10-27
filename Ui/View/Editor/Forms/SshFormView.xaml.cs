using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using _1RM.Model.Protocol;
using _1RM.Utils.KiTTY;
using _1RM.Utils.KiTTY.Model;
using Shawn.Utils.Wpf.FileSystem;

namespace _1RM.View.Editor.Forms
{
    public partial class SshFormView : FormBase
    {
        public SshFormView() : base()
        {
            InitializeComponent();
            Loaded += (sender, args) =>
            {
                if (DataContext is SSH ssh)
                {
                    CbUsePrivateKey.IsChecked = false;
                    if (ssh.PrivateKey == ssh.ServerEditorDifferentOptions)
                    {
                        CbUsePrivateKey.IsChecked = null;
                    }

                    if (!string.IsNullOrEmpty(ssh.PrivateKey))
                    {
                        CbUsePrivateKey.IsChecked = true;
                    }
                }
            };
        }

        private void ButtonOpenPrivateKey_OnClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is SSH ssh)
            {
                    var path = SelectFileHelper.OpenFile(filter: "ppk|*.*", currentDirectoryForShowingRelativePath: Environment.CurrentDirectory);
                if (path == null) return;
                ssh.PrivateKey = path;
            }
        }

        private void CbUsePrivateKey_OnChecked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && DataContext is SSH ssh)
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
            if (DataContext is IKittyConnectable pc)
            {
                var path = SelectFileHelper.OpenFile(filter: "KiTTY Session|*.*");
                if (path == null) return;
                if (File.Exists(path)
                && KittyConfig.Read(path)?.Count > 0)
                {
                    pc.ExternalKittySessionConfigPath = path;
                }
                else
                {
                    pc.ExternalKittySessionConfigPath = "";
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
