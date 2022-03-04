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
using PRM.Model;
using PRM.View;
using Shawn.Utils;

namespace PRM.Controls
{
    /// <summary>
    /// Interaction logic for ServerListItem.xaml
    /// </summary>
    public partial class ServerListItem : UserControl
    {
        public static readonly DependencyProperty ProtocolServerViewModelProperty =
            DependencyProperty.Register("ServerViewModel", typeof(ServerViewModel), typeof(ServerListItem),
                new PropertyMetadata(null, new PropertyChangedCallback(OnDataChanged)));

        private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var value = (ServerViewModel)e.NewValue;
            ((ServerListItem)d).DataContext = value;
        }

        public ServerViewModel ServerViewModel
        {
            get => (ServerViewModel)GetValue(ProtocolServerViewModelProperty);
            set => SetValue(ProtocolServerViewModelProperty, value);
        }

        public ServerListItem()
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
