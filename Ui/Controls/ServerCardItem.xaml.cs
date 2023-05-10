using System.Windows;
using System.Windows.Controls;
using _1RM.Controls.NoteDisplay;
using _1RM.Model;
using _1RM.View;
using _1RM.View.ServerList;
using Shawn.Utils;

namespace _1RM.Controls
{
    public partial class ServerCardItem : UserControl
    {
        public static readonly DependencyProperty ProtocolServerViewModelProperty =
            DependencyProperty.Register("ProtocolBaseViewModel", typeof(ProtocolBaseViewModel), typeof(ServerCardItem),
                new PropertyMetadata(null, new PropertyChangedCallback(OnServerDataChanged)));

        private static void OnServerDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var value = (ProtocolBaseViewModel)e.NewValue;
            ((ServerCardItem)d).DataContext = value;
            if (value?.HoverNoteDisplayControl != null)
            {
                value.HoverNoteDisplayControl.IsBriefNoteShown = false;
            }
        }

        public ProtocolBaseViewModel ProtocolBaseViewModel
        {
            get => (ProtocolBaseViewModel)GetValue(ProtocolServerViewModelProperty);
            set => SetValue(ProtocolServerViewModelProperty, value);
        }

        public ServerCardItem()
        {
            InitializeComponent();
            PopupCardSettingMenu.Closed += (sender, args) =>
            {
                ProtocolBaseViewModel.Actions = null;
            };
        }

        private void BtnSettingMenu_OnClick(object sender, RoutedEventArgs e)
        {
            ProtocolBaseViewModel.Actions = ProtocolBaseViewModel.GetActions();
            PopupCardSettingMenu.IsOpen = true;
        }

        private void ServerMenuButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button { CommandParameter: ProtocolAction afs })
            {
                afs.Run();
            }
            PopupCardSettingMenu.IsOpen = false;
        }

        private void ItemsCheckBox_OnClick(object sender, RoutedEventArgs e)
        {
            ServerListPageView.ItemsCheckBox_OnClick_Static(sender, e);
        }
    }
}