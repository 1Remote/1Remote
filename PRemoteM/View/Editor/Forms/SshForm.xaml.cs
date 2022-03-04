using System;
using System.IO;
using System.Windows;
using System.Windows.Data;
using PRM.Model.Protocol;
using PRM.Model.Protocol.Base;
using PRM.Utils.KiTTY;
using Shawn.Utils.Wpf.FileSystem;

namespace PRM.View.Editor.Forms
{
    public partial class SshForm : FormBase
    {
        public readonly ProtocolBase Vm;
        public SshForm(ProtocolBase vm) : base(vm)
        {
            InitializeComponent();
            Vm = vm;
            DataContext = vm;

            GridUserName.Visibility = Visibility.Collapsed;
            GridPwd.Visibility = Visibility.Collapsed;
            GridPrivateKey.Visibility = Visibility.Collapsed;


            if (Vm.GetType() == typeof(SSH)
                || Vm.GetType().BaseType == typeof(ProtocolBaseWithAddressPortUserPwd))
            {
                GridPrivateKey.Visibility =
                GridUserName.Visibility =
                    GridPwd.Visibility =  Visibility.Visible;
            }


            if (Vm.GetType() == typeof(Telnet)
                || Vm.GetType().BaseType == typeof(ProtocolBaseWithAddressPort))
            {
                
            }

            if (Vm.GetType() == typeof(SSH))
            {
                CbUsePrivateKey.IsChecked = false;
                if (((SSH)Vm).PrivateKey == vm.ServerEditorDifferentOptions)
                {
                    CbUsePrivateKey.IsChecked = null;
                }
                if (!string.IsNullOrEmpty(((SSH)Vm).PrivateKey))
                {
                    CbUsePrivateKey.IsChecked = true;
                }
            }
        }

        private void ButtonOpenPrivateKey_OnClick(object sender, RoutedEventArgs e)
        {
            if (Vm.GetType() == typeof(SSH))
            {
                var path = SelectFileHelper.OpenFile(filter: "ppk|*.*", currentDirectoryForShowingRelativePath: Environment.CurrentDirectory);
                if (path == null) return;
                ((SSH)Vm).PrivateKey = path;
            }
        }

        private void CbUsePrivateKey_OnChecked(object sender, RoutedEventArgs e)
        {
            if (CbUsePrivateKey.IsChecked == false)
            {
                if (Vm.GetType() == typeof(SSH))
                    ((SSH)Vm).PrivateKey = "";
            }
        }

        private void ButtonSelectSessionConfigFile_OnClick(object sender, RoutedEventArgs e)
        {
            if (Vm is IKittyConnectable pc)
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
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return 2;
            return ((int)value - 1).ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var i = int.Parse(value.ToString());
            if (i == 0)
                return 1;
            else if(i == 2)
                return null;
            return 2;
        }
        #endregion
    }
}
