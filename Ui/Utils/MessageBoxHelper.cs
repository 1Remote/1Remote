using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Shawn.Utils.Interface;
using Stylet;

namespace PRM.Utils
{
    public static class MessageBoxHelper
    {
        public static bool ShowConfirmBox(string title, string content)
        {
            string titleText = IoC.Get<ILanguageService>().Translate(title);
            string contentText = IoC.Get<ILanguageService>().Translate(content);
            var ret = IoC.Get<IWindowManager>().ShowMessageBox(contentText, titleText, buttons: MessageBoxButton.YesNo,
                buttonLabels: new Dictionary<MessageBoxResult, string>()
                {
                    { MessageBoxResult.Yes, IoC.Get<ILanguageService>().Translate("Yes") },
                    { MessageBoxResult.No, IoC.Get<ILanguageService>().Translate("Cancel") },
                });
            return ret == MessageBoxResult.Yes;
        }
    }
}
