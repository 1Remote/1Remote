using System.Windows;
using System.Windows.Controls;
using PRM.Model;
using PRM.View;

namespace PRM.Controls
{
    public partial class ServerCard : UserControl
    {
        public static readonly DependencyProperty ProtocolServerViewModelProperty =
            DependencyProperty.Register("ProtocolBaseViewModel", typeof(ProtocolBaseViewModel), typeof(ServerCard),
                new PropertyMetadata(null, new PropertyChangedCallback(OnServerDataChanged)));

        private static void OnServerDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var value = (ProtocolBaseViewModel)e.NewValue;
            ((ServerCard)d).DataContext = value;
        }

        public ProtocolBaseViewModel ProtocolBaseViewModel
        {
            get => (ProtocolBaseViewModel)GetValue(ProtocolServerViewModelProperty);
            set => SetValue(ProtocolServerViewModelProperty, value);
        }

        public ServerCard()
        {
            InitializeComponent();
        }

        private void BtnSettingMenu_OnClick(object sender, RoutedEventArgs e)
        {
            ProtocolBaseViewModel.Actions = ProtocolBaseViewModel.Server.GetActions(IoC.Get<PrmContext>(), RemoteWindowPool.Instance.TabWindowCount);
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
            if (ProtocolBaseViewModel != null && ProtocolBaseViewModel.CmdDuplicateServer.CanExecute())
            {
                ProtocolBaseViewModel.CmdDuplicateServer.Execute();
            }
        }
    }
}