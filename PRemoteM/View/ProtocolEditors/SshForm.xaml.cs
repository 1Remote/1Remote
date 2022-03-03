using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using Microsoft.Win32;
using PRM.Model.Protocol.Base;
using PRM.Model.Protocol.Putty;
using PRM.Utils.KiTTY;
using Shawn.Utils;
using Shawn.Utils.Wpf.FileSystem;

namespace PRM.View.ProtocolEditors
{
    public partial class SshForm : ProtocolServerFormBase
    {
        public readonly ProtocolServerBase Vm;
        public SshForm(ProtocolServerBase vm) : base(vm)
        {
            InitializeComponent();
            Vm = vm;
            DataContext = vm;

            GridUserName.Visibility = Visibility.Collapsed;
            GridPwd.Visibility = Visibility.Collapsed;
            GridPrivateKey.Visibility = Visibility.Collapsed;


            if (Vm.GetType() == typeof(ProtocolServerSSH)
                || Vm.GetType().BaseType == typeof(ProtocolServerWithAddrPortUserPwdBase))
            {
                GridPrivateKey.Visibility =
                GridUserName.Visibility =
                    GridPwd.Visibility =  Visibility.Visible;
            }


            if (Vm.GetType() == typeof(ProtocolServerTelnet)
                || Vm.GetType().BaseType == typeof(ProtocolServerWithAddrPortBase))
            {
                
            }

            if (Vm.GetType() == typeof(ProtocolServerSSH))
            {
                CbUsePrivateKey.IsChecked = false;
                if (((ProtocolServerSSH)Vm).PrivateKey == vm.Server_editor_different_options)
                {
                    CbUsePrivateKey.IsChecked = null;
                }
                if (!string.IsNullOrEmpty(((ProtocolServerSSH)Vm).PrivateKey))
                {
                    CbUsePrivateKey.IsChecked = true;
                }
            }
        }

        private void ButtonOpenPrivateKey_OnClick(object sender, RoutedEventArgs e)
        {
            if (Vm.GetType() == typeof(ProtocolServerSSH))
            {
                var path = SelectFileHelper.OpenFile(filter: "ppk|*.*", currentDirectoryForShowingRelativePath: Environment.CurrentDirectory);
                if (path == null) return;
                ((ProtocolServerSSH)Vm).PrivateKey = path;
            }
        }

        private void CbUsePrivateKey_OnChecked(object sender, RoutedEventArgs e)
        {
            if (CbUsePrivateKey.IsChecked == false)
            {
                if (Vm.GetType() == typeof(ProtocolServerSSH))
                    ((ProtocolServerSSH)Vm).PrivateKey = "";
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
