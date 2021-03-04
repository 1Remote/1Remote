using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using PRM.Core.Protocol.Putty.SSH;
using PRM.Core.Protocol.Putty.Telnet;


namespace PRM.Core.Protocol.Putty
{
    public partial class ProtocolPuttyForm : ProtocolServerFormBase
    {
        public readonly ProtocolServerBase Vm;
        public ProtocolPuttyForm(ProtocolServerBase vm) : base(vm)
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
                var dlg = new OpenFileDialog();
                dlg.Filter = "ppk|*.*";
                if (dlg.ShowDialog() == true)
                {
                    ((ProtocolServerSSH)Vm).PrivateKey = dlg.FileName;
                }
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
            if (Vm is IPuttyConnectable pc)
            {
                var dlg = new OpenFileDialog
                {
                    Filter = "KiTTY Session|*.*",
                    CheckFileExists = true,
                };
                if (dlg.ShowDialog() != true) return;

                var path = dlg.FileName;
                if (File.Exists(path)
                && KittyPortableSessionConfigReader.Read(path)?.Count > 0)
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
            return ((int)((ProtocolServerSSH.ESshVersion)value) - 1).ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (ProtocolServerSSH.ESshVersion)(int.Parse(value.ToString()));
        }
        #endregion
    }
}
