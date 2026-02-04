using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using _1RM.Model;
using _1RM.View;
using _1RM.View.ServerView;
using Shawn.Utils;

namespace _1RM.Controls
{
    /// <summary>
    /// Interaction logic for ServerLineItem.xaml
    /// </summary>
    public partial class ServerLineItem : UserControl
    {
        public static readonly DependencyProperty ProtocolServerViewModelProperty =
            DependencyProperty.Register("ProtocolBaseViewModel", typeof(ProtocolBaseViewModel), typeof(ServerLineItem),
                new PropertyMetadata(null, new PropertyChangedCallback(OnDataChanged)));

        private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var value = (ProtocolBaseViewModel)e.NewValue;
            ((ServerLineItem)d).DataContext = value;
        }

        public ProtocolBaseViewModel? ProtocolBaseViewModel
        {
            get => GetValue(ProtocolServerViewModelProperty) as ProtocolBaseViewModel;
            set => SetValue(ProtocolServerViewModelProperty, value);
        }


        public ServerLineItem()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            PopupCardSettingMenu.Closed += PopupCardSettingMenuOnClosed;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // Unsubscribe from event to prevent memory leak
            PopupCardSettingMenu.Closed -= PopupCardSettingMenuOnClosed;
        }

        private void PopupCardSettingMenuOnClosed(object? sender, EventArgs e)
        {
            ProtocolBaseViewModel?.ClearActions();
        }

        private void BtnSettingMenu_OnClick(object sender, RoutedEventArgs e)
        {
            ProtocolBaseViewModel?.BuildActions();
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

        private void UIElement_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // stop right click edit 
            if (e.ChangedButton == MouseButton.Right)
            {
                e.Handled = true;
            }
        }
    }
}
