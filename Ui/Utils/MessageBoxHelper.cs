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
        public static bool Confirm(string content, string title = "", bool useNativeBox = false, IViewAware? ownerViewModel = null)
        {
            if (string.IsNullOrEmpty(title))
                title = IoC.Get<ILanguageService>().Translate("Warning");
            if (useNativeBox)
            {
                var ret = MessageBoxResult.Yes == MessageBox.Show(content, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
                return ret;
            }
            else
            {
                var id = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                GlobalEventHelper.ProcessingRingInvoke?.Invoke(id, Visibility.Visible, "");
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
                IoC.Get<IWindowManager>().ShowDialog(vm, ownerViewModel);
                var ret = MessageBoxResult.Yes == vm.ClickedButton;
                GlobalEventHelper.ProcessingRingInvoke?.Invoke(id, Visibility.Collapsed, "");
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

        private static void Alert(string title, string content, MessageBoxImage icon, bool useNativeBox, IViewAware? ownerViewModel = null)
        {
            Execute.OnUIThreadSync(() =>
            {
                if (useNativeBox)
                {
                    MessageBox.Show(content, title, MessageBoxButton.OK, icon);
                }
                //else if (ownerViewModel is IMaskLayerContainer mvm)
                //{
                //    var vm = new _1RM.View.Utils.MessageBoxPageViewModel();
                //    vm.Setup(messageBoxText: content,
                //        caption: title,
                //        icon: icon,
                //        buttons: MessageBoxButton.OK,
                //        buttonLabels: new Dictionary<MessageBoxResult, string>()
                //        {
                //            {MessageBoxResult.None, IoC.Get<ILanguageService>().Translate("OK")},
                //            {MessageBoxResult.Yes, IoC.Get<ILanguageService>().Translate("OK")},
                //            {MessageBoxResult.OK, IoC.Get<ILanguageService>().Translate("OK")},
                //        }, onButtonClicked: () =>
                //        {
                //            mvm.TopLevelViewModel = null;
                //        });
                //    mvm.TopLevelViewModel = vm;
                //}
                else
                {
                    var id = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    GlobalEventHelper.ProcessingRingInvoke?.Invoke(id, Visibility.Visible, "");
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
                    IoC.Get<IWindowManager>().ShowDialog(vm, ownerViewModel);
                    GlobalEventHelper.ProcessingRingInvoke?.Invoke(id, Visibility.Collapsed, "");
                }
            });
        }
    }
}
