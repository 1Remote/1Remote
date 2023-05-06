using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using _1RM.View;
using _1RM.View.Utils;
using Shawn.Utils.Interface;
using Stylet;
using Screen = Stylet.Screen;

namespace _1RM.Utils
{
    public static class MessageBoxHelper
    {
        [DllImport("user32.dll")]
        private static extern int EnableWindow(IntPtr handle, bool enable);
        /// <summary>
        /// show a confirm box on owner, the default value owner is MainWindowViewModel
        /// </summary>
        public static bool Confirm(string content, string title = "", bool useNativeBox = false, object? ownerViewModel = null)
        {
            return Confirm(content,
                IoC.Get<ILanguageService>().Translate("OK"),
                IoC.Get<ILanguageService>().Translate("Cancel"),
                title, useNativeBox, ownerViewModel);
        }

        /// <summary>
        /// show a confirm box on owner, the default value owner is MainWindowViewModel
        /// </summary>
        public static bool Confirm(string content, string yesButtonText, string noButtonText, string title = "", bool useNativeBox = false, object? ownerViewModel = null)
        {
            if (string.IsNullOrEmpty(title))
                title = IoC.Get<ILanguageService>().Translate("Warning");

            var ownerViewAware = (ownerViewModel as IViewAware);
            var mainWindowViewModel = IoC.TryGet<MainWindowViewModel>();
            if (useNativeBox
                || ownerViewModel != null && ownerViewAware == null     // 设定了 owner 且 owner 不是 IViewAware
                || ownerViewModel == null && mainWindowViewModel?.View is Window { ShowInTaskbar: false }) // 未设定 owner 且 MainWindow is hidden
            {
                var ret = MessageBoxResult.Yes == MessageBox.Show(content, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
                return ret;
            }
            else
            {
                IMaskLayerContainer? layerContainer = null;
                if (ownerViewModel is IMaskLayerContainer mlc)
                {
                    layerContainer = mlc;
                }
                else if (ownerViewModel == null)
                {
                    layerContainer = mainWindowViewModel;
                }
                long layerId = 0;
                if (layerContainer != null)
                    layerId = MaskLayerController.ShowProcessingRing(assignLayerContainer: layerContainer);
                var vm = IoC.Get<IMessageBoxViewModel>();
                vm.Setup(messageBoxText: content,
                    caption: title,
                    icon: MessageBoxImage.Question,
                    buttons: MessageBoxButton.YesNo,
                    buttonLabels: new Dictionary<MessageBoxResult, string>()
                    {
                        { MessageBoxResult.Yes, yesButtonText},
                        { MessageBoxResult.No, noButtonText},
                    });
                if (vm is Screen screen)
                {
                    screen.Activated += MessageBoxOnActivated;
                }
                Execute.OnUIThreadSync(() =>
                {
                    IoC.Get<IWindowManager>().ShowDialog(vm, ownerViewAware ?? mainWindowViewModel);
                });
                var ret = MessageBoxResult.Yes == vm.ClickedButton;
                if (layerContainer != null)
                    MaskLayerController.HideMask(layerId, layerContainer);
                return ret;
            }
        }

        private static void MessageBoxOnActivated(object? sender, ActivationEventArgs e)
        {
            if (sender is Screen { View: Window { Owner: { }, IsLoaded: false } dlgWindow })
            {
                dlgWindow.Loaded -= MessageBoxOnLoaded;
                dlgWindow.Loaded += MessageBoxOnLoaded;
            }
        }

        private static void MessageBoxOnLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is Window dlgWindow)
            {
                var windows = Application.Current.Windows;
                // enable the window != owner to let message box freeze the owner only.
                foreach (Window w in windows)
                {
                    if (w == dlgWindow.Owner) continue;
                    if (w is { IsLoaded: true })
                    {
                        if (HwndSource.FromVisual(w) is HwndSource hwndSource)
                            EnableWindow(hwndSource.Handle, true);
                    }
                }
            }
        }

        public static void Info(string content, string title = "", bool useNativeBox = false, IViewAware? ownerViewModel = null)
        {
            if (string.IsNullOrEmpty(title))
                title = IoC.Get<ILanguageService>().Translate("Info");
            Alert(title, content, MessageBoxImage.Information, useNativeBox, ownerViewModel);
        }

        public static void Warning(string content, string title = "", bool useNativeBox = false, IViewAware? ownerViewModel = null)
        {
            if (string.IsNullOrEmpty(title))
                title = IoC.Get<ILanguageService>().Translate("Warning");
            Alert(title, content, MessageBoxImage.Warning, useNativeBox, ownerViewModel);
        }


        public static void ErrorAlert(string content, string title = "", bool useNativeBox = false, IViewAware? ownerViewModel = null)
        {
            if (string.IsNullOrEmpty(title))
                title = IoC.Get<ILanguageService>().Translate("Error");
            Alert(title, content, MessageBoxImage.Error, useNativeBox, ownerViewModel);
        }

        private static void Alert(string title, string content, MessageBoxImage icon, bool useNativeBox, object? ownerViewModel = null)
        {
            Execute.OnUIThreadSync(() =>
            {
                var ownerViewAware = (ownerViewModel as IViewAware);
                var mainWindowViewModel = IoC.TryGet<MainWindowViewModel>();
                if (useNativeBox
                    || ownerViewModel != null && ownerViewAware == null     // 设定了 owner 且 owner 不是 IViewAware
                    || ownerViewModel == null && mainWindowViewModel?.View is Window { ShowInTaskbar: false }) // 未设定 owner 且 MainWindow is hidden
                {
                    MessageBox.Show(content, title, MessageBoxButton.OK, icon);
                }
                else
                {
                    IMaskLayerContainer? layerContainer = null;
                    if (ownerViewModel is IMaskLayerContainer mlc)
                    {
                        layerContainer = mlc;
                    }
                    else if (ownerViewModel == null)
                    {
                        layerContainer = mainWindowViewModel;
                    }
                    long layerId = 0;
                    if (layerContainer != null)
                        layerId = MaskLayerController.ShowProcessingRing(assignLayerContainer: layerContainer);
                    var vm = IoC.Get<IMessageBoxViewModel>();
                    vm.Setup(messageBoxText: content,
                        caption: title,
                        icon: icon,
                        buttons: MessageBoxButton.OK,
                        buttonLabels: new Dictionary<MessageBoxResult, string>()
                        {
                            {MessageBoxResult.None, IoC.Get<ILanguageService>().Translate("OK")},
                            {MessageBoxResult.Yes, IoC.Get<ILanguageService>().Translate("OK")},
                            {MessageBoxResult.OK, IoC.Get<ILanguageService>().Translate("OK")},
                        });
                    if (vm is Screen screen)
                    {
                        screen.Activated += (sender, args) =>
                        {
                            if (screen.View is Window dlgWindow)
                            {
                                // dlg don't have a Owner
                                if (dlgWindow?.Owner == null)
                                    return;
                                var windows = Application.Current.Windows;
                                // enable the window != owner to let message box freeze the owner only.
                                foreach (Window w in windows)
                                {
                                    if (w == dlgWindow.Owner) continue;
                                    if (w is { IsLoaded: true })
                                    {
                                        if (HwndSource.FromVisual(w) is HwndSource hwndSource)
                                            EnableWindow(hwndSource.Handle, true);
                                    }
                                }
                            }
                        };
                    }
                    IoC.Get<IWindowManager>().ShowDialog(vm, ownerViewAware ?? mainWindowViewModel);
                    if (layerContainer != null)
                        MaskLayerController.HideMask(layerId, layerContainer: layerContainer);
                }
            });
        }
    }
}
