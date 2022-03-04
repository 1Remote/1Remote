using System.Windows;
using System.Windows.Controls;
using PRM.Model;
using PRM.View;

namespace PRM.Controls
{
    public partial class ServerCard : UserControl
    {
        public static readonly DependencyProperty ProtocolServerViewModelProperty =
            DependencyProperty.Register("ServerViewModel", typeof(ServerViewModel), typeof(ServerCard),
                new PropertyMetadata(null, new PropertyChangedCallback(OnServerDataChanged)));

        private static void OnServerDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var value = (ServerViewModel)e.NewValue;
            ((ServerCard)d).DataContext = value;
        }

        public ServerViewModel ServerViewModel
        {
            get => (ServerViewModel)GetValue(ProtocolServerViewModelProperty);
            set => SetValue(ProtocolServerViewModelProperty, value);
        }

        public ServerCard()
        {
            InitializeComponent();
        }

        private void BtnSettingMenu_OnClick(object sender, RoutedEventArgs e)
        {
            ServerViewModel.Actions = ServerViewModel.Server.GetActions(App.Context, RemoteWindowPool.Instance.TabWindowCount);
            PopupCardSettingMenu.IsOpen = true;
        }

        private void ServerMenuButton_OnClick(object sender, RoutedEventArgs e)
        {
            PopupCardSettingMenu.IsOpen = false;
            if (sender is Button { CommandParameter: ProtocolAction afs })
            {
                afs.Run();
            }
        }

        private void ButtonDuplicateServer_OnClick(object sender, RoutedEventArgs e)
        {
            PopupCardSettingMenu.IsOpen = false;
            if (ServerViewModel != null && ServerViewModel.CmdDuplicateServer.CanExecute())
            {
                ServerViewModel.CmdDuplicateServer.Execute();
            }
        }
    }
}