using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using _1RM.Model;
using _1RM.View;
using Shawn.Utils.Interface;
using Stylet;

namespace _1RM.Utils
{
    public static class MessageBoxHelper
    {
        /// <summary>
        /// show a confirm box on owner, the default value owner is MainWindowViewModel
        /// </summary>
        /// <returns></returns>
        public static bool Confirm(string content, string title = "", bool useNativeBox = false, object? ownerViewModel = null)
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
                var layerContainer = ownerViewModel as IMaskLayerContainer;
                long layerId = 0;
                if (layerContainer != null)
                    layerId = MaskLayerController.ShowProcessingRing(layerContainer: layerContainer);
                var vm = IoC.Get<IMessageBoxViewModel>();
                vm.Setup(messageBoxText: content,
                    caption: title,
                    icon: MessageBoxImage.Question,
                    buttons: MessageBoxButton.YesNo,
                    buttonLabels: new Dictionary<MessageBoxResult, string>()
                    {
                        { MessageBoxResult.Yes, IoC.Get<ILanguageService>().Translate("OK") },
                        { MessageBoxResult.No, IoC.Get<ILanguageService>().Translate("Cancel") },
                    });
                IoC.Get<IWindowManager>().ShowDialog(vm, ownerViewModel != null ? ownerViewAware : mainWindowViewModel);
                var ret = MessageBoxResult.Yes == vm.ClickedButton;
                if (layerContainer != null)
                    MaskLayerController.HideProcessingRing(layerId, layerContainer: layerContainer);
                return ret;
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
                    var layerContainer = ownerViewModel as IMaskLayerContainer;
                    long layerId = 0;
                    if (layerContainer != null)
                        layerId = MaskLayerController.ShowProcessingRing(layerContainer: layerContainer);
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
                    IoC.Get<IWindowManager>().ShowDialog(vm, ownerViewModel != null ? ownerViewAware : mainWindowViewModel);
                    if (layerContainer != null)
                        MaskLayerController.HideProcessingRing(layerId, layerContainer: layerContainer);
                }
            });
        }
    }
}
