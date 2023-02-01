﻿using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using _1RM.Resources.Icons;
using _1RM.Service.DataSource;
using _1RM.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.Image;

namespace _1RM.View.Editor
{
    public partial class ServerEditorPageView : UserControl
    {
        private BitmapSource? _oldLogo;

        public ServerEditorPageView()
        {
            this.Loaded += (sender, args) =>
            {
                if (this.DataContext is ServerEditorPageViewModel vm)
                {
                    // add mode
                    if (vm.IsAddMode)
                    {
                        ButtonSave.Content = IoC.Get<ILanguageService>().Translate("Add");
                        if (vm.Server.IconImg == null
                            && ServerIcons.Instance.IconsBase64.Count > 0)
                        {
                            var r = new Random(DateTime.Now.Millisecond);
                            vm.Server.IconBase64 = ServerIcons.Instance.IconsBase64[r.Next(0, ServerIcons.Instance.IconsBase64.Count)];
                        }
                    }
                    _oldLogo = vm.Server.IconImg;
                    LogoSelector.SetImg(vm.Server.IconImg);
                    LogoSelector.OnLogoChanged += () => vm.Server.IconBase64 = LogoSelector.Logo.ToBase64();
                }
            };
        }
        ~ServerEditorPageView()
        {
            Console.WriteLine($"Release {this.GetType().Name}({this.GetHashCode()})");
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
            if (this.DataContext is ServerEditorPageViewModel vm)
            {
                vm.Server.IconBase64 = _oldLogo.ToBase64();
                PopupLogoSelectorClose();
            }
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

        private void LogoList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (this.DataContext is ServerEditorPageViewModel vm)
                vm.Server.IconBase64 = LogoSelector.Logo.ToBase64();
            PopupLogoSelectorClose();
        }

        private void ButtonShowNote_OnClick(object sender, RoutedEventArgs e)
        {
            PopupNote.IsOpen = false;
            PopupNote.IsOpen = true;
        }

        private void TextBoxMarkdown_OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
        }
    }
}