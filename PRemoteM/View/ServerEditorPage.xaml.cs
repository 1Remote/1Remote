using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using PRM.Core.Model;
using PRM.Core.Protocol;
using Shawn.Utils;
using PRM.ViewModel;

namespace PRM.View
{
    public partial class ServerEditorPage : UserControl
    {
        public readonly VmServerEditorPage Vm;
        private readonly BitmapSource _oldLogo;

        public ServerEditorPage(VmServerEditorPage vm)
        {
            Debug.Assert(vm?.Server != null);
            InitializeComponent();
            Vm = vm;
            DataContext = vm;
            // add mode
            if (vm.IsAddMode)
            {
                ButtonSave.Content = SystemConfig.Instance.Language.GetText("word_add");
                ColorPick.Color = ColorAndBrushHelper.HexColorToMediaColor(SystemConfig.Instance.Theme.MainColor1);

                if (vm.Server.IconImg == null 
                    && ServerIcons.Instance.Icons.Count > 0)
                {
                    var r = new Random(DateTime.Now.Millisecond);
                    vm.Server.IconBase64 = ServerIcons.Instance.Icons[r.Next(0, ServerIcons.Instance.Icons.Count)].ToBase64();
                }
            }


            _oldLogo = vm.Server.IconImg;
            LogoSelector.SetImg(vm.Server.IconImg);
            LogoSelector.OnLogoChanged += () => Vm.Server.IconBase64 = LogoSelector.Logo.ToBase64();
        }

        private void ImgLogo_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (PopupLogoSelector.Height > 0)
                PopupLogoSelectorClose();
            else
                PopupLogoSelectorOpen();
        }

        private void ButtonLogoSave_OnClick(object sender, RoutedEventArgs e)
        {
            PopupLogoSelectorClose();
        }

        private void ButtonLogoCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Vm.Server.IconBase64 = _oldLogo.ToBase64();
            PopupLogoSelectorClose();
        }

        private void PopupLogoSelectorHeightAnimation(double to)
        {
            var animation = new DoubleAnimation
            {
                From = PopupLogoSelector.Height,
                To = to,
                Duration = new Duration(TimeSpan.FromMilliseconds(350)),
                AccelerationRatio = 0.9,
            };
            PopupLogoSelector.BeginAnimation(HeightProperty, null);
            PopupLogoSelector.BeginAnimation(HeightProperty, animation);
        }

        private void PopupLogoSelectorOpen()
        {
            if (Math.Abs(PopupLogoSelector.Height) < 1)
            {
                PopupLogoSelectorHeightAnimation(197);
            }
        }

        private void PopupLogoSelectorClose()
        {
            if (PopupLogoSelector.Height > 0.5)
            {
                PopupLogoSelectorHeightAnimation(0);
            }
        }

        private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LogoSelector.SetImg((BitmapSource)(((ListView)sender).SelectedItem));
        }

        private void ButtonTryCommandBeforeConnected_OnClick(object sender, RoutedEventArgs e)
        {
            TryCmd(Vm.Server.CommandBeforeConnected);
        }

        private void ButtonTryCommandAfterDisconnected_OnClick(object sender, RoutedEventArgs e)
        {
            TryCmd(Vm.Server.CommandAfterDisconnected);
        }

        private static void TryCmd(string cmd)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(cmd))
                {
                    // TODO add some params
                    CmdRunner.RunCmdAsync(cmd);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
            }
        }

        private void LogoList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (Vm?.Server != null)
            {
                Vm.Server.IconBase64 = LogoSelector.Logo.ToBase64();
            }
            PopupLogoSelectorClose();
        }
    }
}