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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using PersonalRemoteManager;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.Protocol.RDP;
using PRM.ViewModel;
using Shawn.Utils;

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
                ButtonSave.Content = SystemConfig.Instance.Language.GetText("button_add");
                ColorPick.Color = ColorAndBrushHelper.HexColorToMediaColor(SystemConfig.Instance.Theme.MainColor1);
            }

            if (vm.Server.IconImg == null)
            {
                ButtonSave.Content = SystemConfig.Instance.Language.GetText("button_add");
                if (ServerIcons.Instance.Icons.Count > 0)
                {
                    var r = new Random(DateTime.Now.Millisecond);
                    vm.Server.IconImg = ServerIcons.Instance.Icons[r.Next(0, ServerIcons.Instance.Icons.Count)];
                }
            }

            _oldLogo = vm.Server.IconImg;
            LogoSelector.SetImg(vm.Server.IconImg);
            LogoSelector.OnLogoChanged += () => Vm.Server.IconImg = LogoSelector.Logo;
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
            if (Vm?.Server != null && Vm.Server.GetType() != typeof(ProtocolServerNone))
            {
                Vm.Server.IconImg = LogoSelector.Logo;
            }
            PopupLogoSelectorClose();
        }

        private void ButtonLogoCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Vm.Server.IconImg = _oldLogo;
            PopupLogoSelectorClose();
        }
        
        private void PopupLogoSelectorOpen()
        {
            if (Math.Abs(PopupLogoSelector.Height) < 1)
            {
                var animation = new DoubleAnimation
                {
                    From = PopupLogoSelector.Height,
                    To = 197,
                    Duration = new Duration(TimeSpan.FromMilliseconds(350)),
                    AccelerationRatio = 0.9,
                };
                PopupLogoSelector.BeginAnimation(HeightProperty, null);
                PopupLogoSelector.BeginAnimation(HeightProperty, animation);
            }
        }

        private void PopupLogoSelectorClose()
        {
            if (PopupLogoSelector.Height > 0.5)
            {
                var animation = new DoubleAnimation
                {
                    From = PopupLogoSelector.Height,
                    To = 0,
                    Duration = new Duration(TimeSpan.FromMilliseconds(350)),
                    DecelerationRatio = 0.9,
                };
                PopupLogoSelector.BeginAnimation(HeightProperty, null);
                PopupLogoSelector.BeginAnimation(HeightProperty, animation);
            }
        }
        private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LogoSelector.SetImg((BitmapSource) (((ListView) sender).SelectedItem));
        }
    }
}
