using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using PRM.ViewModel;

namespace PRM.Controls
{
    /// <summary>
    /// ServerCard.xaml 的交互逻辑
    /// </summary>
    public partial class ServerCard : UserControl
    {
        public static readonly DependencyProperty VmServerCardProperty =
            DependencyProperty.Register("VmServerCard", typeof(VmServerCard), typeof(ServerCard),
                new PropertyMetadata(new VmServerCard(null, null), new PropertyChangedCallback(OnServerDataChanged)));

        private static void OnServerDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var value = (VmServerCard)e.NewValue;
            ((ServerCard)d).DataContext = value;
        }
        public VmServerCard VmServerCard
        {
            get => (VmServerCard)GetValue(VmServerCardProperty);
            set => SetValue(VmServerCardProperty, value);
        }

        public ServerCard()
        {
            InitializeComponent();
        }

        private void BtnSettingMenu_OnClick(object sender, RoutedEventArgs e)
        {
            PopupMenu.IsOpen = true;
        }

        private void ButtonEditServer_OnClick(object sender, RoutedEventArgs e)
        {
            PopupMenu.IsOpen = false;
            if (VmServerCard != null && VmServerCard.CmdEditServer.CanExecute())
            {
                VmServerCard.CmdEditServer.Execute();
            }
        }

        private void ButtonExportToFile_OnClick(object sender, RoutedEventArgs e)
        {
            PopupMenu.IsOpen = false;
            var dlg = new SaveFileDialog
            {
                Filter = "PRM json|*.prmj",
                FileName = VmServerCard.Server.DispName + "_" + DateTime.Now.ToString("yyyyMMddhhmmss") + ".prmj"
            };
            if (dlg.ShowDialog() == true)
            {
                File.WriteAllText(dlg.FileName, VmServerCard.Server.ToJsonString());
            }
        }
    }
}
