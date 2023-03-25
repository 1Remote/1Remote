using Stylet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using _1RM.Utils;
using Shawn.Utils.Wpf;
using _1RM.Service;
using Shawn.Utils.Wpf.Controls;

namespace _1RM.View.Utils
{
    /// <summary>
    /// Default implementation of IMessageBoxViewModel, and is therefore the ViewModel shown by default by ShowMessageBox
    /// </summary>
    public class BreakingChangeUpdateViewModel : NotifyPropertyChangedBaseScreen
    {
        public AboutPageViewModel AboutPageViewModel => IoC.Get<AboutPageViewModel>();


        private RelayCommand? _cmdUpdate;
        public RelayCommand CmdUpdate
        {
            get
            {
                return _cmdUpdate ??= new RelayCommand((o) =>
                {
#if FOR_MICROSOFT_STORE_ONLY
                        HyperlinkHelper.OpenUriBySystem("ms-windows-store://review/?productid=9PNMNF92JNFP");
#else
                    HyperlinkHelper.OpenUriBySystem(AboutPageViewModel.NewVersionUrl);
#endif
                });
            }
        }
        private RelayCommand? _cmdClose;
        public RelayCommand CmdClose
        {
            get
            {
                return _cmdClose ??= new RelayCommand((o) =>
                {
                    IoC.Get<ConfigurationService>().Engagement.BreakingChangeAlertVersionString = AboutPageViewModel.NewVersion;
                    IoC.Get<ConfigurationService>().Save();
                    this.RequestClose(true);
                });
            }
        }
    }
}
