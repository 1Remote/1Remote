using System.Windows;
using System.Windows.Controls;
using PRM.Core.Protocol;
using PRM.ViewModel;

namespace PRM.Controls
{
    public partial class ServerCard : UserControl
    {
        public static readonly DependencyProperty VmProtocolServerProperty =
            DependencyProperty.Register("VmProtocolServer", typeof(VmProtocolServer), typeof(ServerCard),
                new PropertyMetadata(null, new PropertyChangedCallback(OnServerDataChanged)));

        private static void OnServerDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var value = (VmProtocolServer)e.NewValue;
            ((ServerCard)d).DataContext = value;
        }

        public VmProtocolServer VmProtocolServer
        {
            get => (VmProtocolServer)GetValue(VmProtocolServerProperty);
            set => SetValue(VmProtocolServerProperty, value);
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
            if (VmProtocolServer != null && VmProtocolServer.CmdEditServer.CanExecute())
            {
                VmProtocolServer.CmdEditServer.Execute();
            }
        }

        private void ButtonDuplicateServer_OnClick(object sender, RoutedEventArgs e)
        {
            PopupCardSettingMenu.IsOpen = false;
            if (VmProtocolServer != null && VmProtocolServer.CmdDuplicateServer.CanExecute())
            {
                VmProtocolServer.CmdDuplicateServer.Execute();
            }
        }
    }
}