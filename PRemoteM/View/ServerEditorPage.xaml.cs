using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.Protocol.RDP;
using PRM.ViewModel;

namespace PRM.View
{
    /// <summary>
    /// ServerEditorPage.xaml 的交互逻辑
    /// </summary>
    public partial class ServerEditorPage : UserControl
    {
        private readonly VmServerEditorPage _vmServerEditorPage;
        public ServerEditorPage(VmServerEditorPage vm)
        {
            Debug.Assert(vm?.Server != null);
            InitializeComponent();
            _vmServerEditorPage = vm;
            DataContext = vm;
            // edit mode
            if (vm.Server.Id > 0 && vm.Server.GetType() != typeof(ProtocolServerNone))
            {
                LogoSelector.SetImg(vm.Server.IconImg);
                ColorPick.Color = vm.Server.MarkColor;
            }
            else
            // add mode
            {
                ButtonSave.Content = SystemConfig.GetInstance().Language.GetText("button_add");
                if (ProtocolServerIcons.Instance.Icons.Count > 0)
                {
                    var r = new Random(DateTime.Now.Millisecond);
                    LogoSelector.Logo =
                    vm.Server.IconImg = ProtocolServerIcons.Instance.Icons[r.Next(0, ProtocolServerIcons.Instance.Icons.Count)];
                }
                // todo use dynamic resource
                ColorPick.Color = (Color)ColorConverter.ConvertFromString("#102b3e");
            }
            ColorPick.OnColorSelected += color => _vmServerEditorPage.Server.MarkColor = ColorPick.Color;
        }

        private void ImgLogo_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            PopupLogoSelector.IsOpen = true;
            ScrollViewerMain.IsEnabled = false;
            ScrollViewerMain.CanContentScroll = false;
        }

        private void ButtonLogoSave_OnClick(object sender, RoutedEventArgs e)
        {
            if (_vmServerEditorPage?.Server != null && _vmServerEditorPage.Server.GetType() != typeof(ProtocolServerNone))
            {
                _vmServerEditorPage.Server.IconImg = LogoSelector.Logo;
                //File.WriteAllText("img.txt",_vmServerEditorPage.Server.IconBase64);
            }
            PopupLogoSelector.IsOpen = false;
            ScrollViewerMain.CanContentScroll = true;
            ScrollViewerMain.IsEnabled = true;
        }

        private void ButtonLogoCancel_OnClick(object sender, RoutedEventArgs e)
        {
            PopupLogoSelector.IsOpen = false;
            ScrollViewerMain.CanContentScroll = true;
            ScrollViewerMain.IsEnabled = true;
        }

        private void UIElement_DisabledOnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
        }

        private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LogoSelector.Logo = (BitmapSource) (((ListView) sender).SelectedItem);
        }
    }
}
