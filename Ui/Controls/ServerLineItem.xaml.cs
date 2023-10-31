﻿using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using _1RM.Model;
using _1RM.View;
using _1RM.View.ServerList;
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
            PopupCardSettingMenu.Closed += (sender, args) =>
            {
                ProtocolBaseViewModel?.ClearActions();
            };
            CbSelected.Visibility = Visibility.Collapsed;
            BtnSettingMenu.Visibility = Visibility.Collapsed;
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

        private void Grid_OnMouseEnter(object sender, MouseEventArgs e)
        {
            //SimpleLogHelper.Debug("Grid_OnMouseEnter");
            CbSelected.Visibility = Visibility.Visible;
            BtnSettingMenu.Visibility = Visibility.Visible;
        }

        private void Grid_OnMouseLeave(object sender, MouseEventArgs e)
        {
            //SimpleLogHelper.Debug("Grid_OnMouseLeave");
            CbSelected.Visibility = Visibility.Collapsed;
            BtnSettingMenu.Visibility = Visibility.Collapsed;
        }
    }
}
