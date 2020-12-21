using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.ViewModel;

namespace PRM.Controls
{
    public partial class ServerCard : UserControl
    {
        public static readonly DependencyProperty VmServerListItemProperty =
            DependencyProperty.Register("VmServerListItem", typeof(VmServerListItem), typeof(ServerCard),
                new PropertyMetadata(new VmServerListItem(null), new PropertyChangedCallback(OnServerDataChanged)));

        private static void OnServerDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var value = (VmServerListItem)e.NewValue;
            ((ServerCard)d).DataContext = value;
        }
        public VmServerListItem VmServerListItem
        {
            get => (VmServerListItem)GetValue(VmServerListItemProperty);
            set => SetValue(VmServerListItemProperty, value);
        }

        public ServerCard()
        {
            InitializeComponent();
        }

        private void BtnSettingMenu_OnClick(object sender, RoutedEventArgs e)
        {
            PopupCardSettingMenu.IsOpen = true;
        }

        private void ButtonEditServer_OnClick(object sender, RoutedEventArgs e)
        {
            PopupCardSettingMenu.IsOpen = false;
            if (VmServerListItem != null && VmServerListItem.CmdEditServer.CanExecute())
            {
                VmServerListItem.CmdEditServer.Execute();
            }
        }

        private void ButtonDuplicateServer_OnClick(object sender, RoutedEventArgs e)
        {
            PopupCardSettingMenu.IsOpen = false;
            if (VmServerListItem != null && VmServerListItem.CmdDuplicateServer.CanExecute())
            {
                VmServerListItem.CmdDuplicateServer.Execute();
            }
        }

        private void ButtonExportToFile_OnClick(object sender, RoutedEventArgs e)
        {
            PopupCardSettingMenu.IsOpen = false;
            var dlg = new SaveFileDialog
            {
                Filter = "PRM json|*.prmj",
                FileName = VmServerListItem.Server.DispName + "_" + DateTime.Now.ToString("yyyyMMddhhmmss") + ".prmj"
            };
            if (dlg.ShowDialog() == true)
            {
                var server = (ProtocolServerBase)VmServerListItem.Server.Clone();
                SystemConfig.Instance.DataSecurity.DecryptPwd(server);
                File.WriteAllText(dlg.FileName, server.ToJsonString(), Encoding.UTF8);
            }
        }
    }
}
