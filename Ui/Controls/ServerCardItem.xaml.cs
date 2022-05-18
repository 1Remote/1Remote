using System.Windows;
using System.Windows.Controls;
using PRM.Model;
using PRM.View;

namespace PRM.Controls
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
        }

        public ProtocolBaseViewModel ProtocolBaseViewModel
        {
            get => (ProtocolBaseViewModel)GetValue(ProtocolServerViewModelProperty);
            set => SetValue(ProtocolServerViewModelProperty, value);
        }

        public ServerCardItem()
        {
            InitializeComponent();
            this.MouseMove += (sender, args) =>
            {
                if (PopupNote.IsOpen == false) return;
                if (ButtonShowNote.ActualWidth > 0)
                {
                    var p1 = args.MouseDevice.GetPosition(ButtonShowNote);
                    //SimpleLogHelper.Debug($"ButtonShowNote: {p1.X}, {p1.Y}");
                    if (p1.X < 0 || p1.Y < 0)
                        PopupNote.IsOpen = false;
                    else if (p1.Y < ButtonShowNote.ActualHeight && p1.X > ButtonShowNote.ActualWidth)
                        PopupNote.IsOpen = false;
                    else if (p1.Y >= ButtonShowNote.ActualHeight)
                    {
                        var p3 = args.MouseDevice.GetPosition(PopupNoteGrid);
                        if (p3.X > PopupNoteGrid.ActualWidth)
                            PopupNote.IsOpen = false;
                        if (p3.Y > PopupNoteGrid.ActualHeight)
                            PopupNote.IsOpen = false;
                    }
                }
            };
            ButtonShowNote.MouseEnter += (sender, args) =>
            {
                PopupNote.IsOpen = false;
                PopupNote.IsOpen = true;
            };
        }

        private void BtnSettingMenu_OnClick(object sender, RoutedEventArgs e)
        {
            ProtocolBaseViewModel.Actions = ProtocolBaseViewModel.Server.GetActions();
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
        private void BtnShowNote_OnClick(object sender, RoutedEventArgs e)
        {
            PopupNote.IsOpen = true;
        }
    }
}