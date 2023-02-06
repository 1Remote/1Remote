using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using _1RM.Service;
using _1RM.Utils;
using _1RM.View.Utils;
using Shawn.Utils;
using Shawn.Utils.Wpf;

namespace _1RM.View
{
    public class RequestRatingViewModel : MaskLayer
    {
        private bool _doNotShowAgain;

        public bool DoNotShowAgain
        {
            get => _doNotShowAgain;
            set => SetAndNotifyIfChanged(ref _doNotShowAgain, value);
        }



        private RelayCommand? _cmdClose;
        public RelayCommand CmdClose
        {
            get
            {
                return _cmdClose ??= new RelayCommand((o) =>
                {
                    IoC.Get<MainWindowViewModel>().TopLevelViewModel = null;
                    IoC.Get<MainWindowViewModel>().HideMe();
                    IoC.Get<ConfigurationService>().Engagement.ConnectCount = -100;
                    if (DoNotShowAgain)
                    {
                        IoC.Get<ConfigurationService>().Engagement.DoNotShowAgain = true;
                        IoC.Get<ConfigurationService>().Engagement.DoNotShowAgainVersionString = AppVersion.Version;
                    }
                    IoC.Get<ConfigurationService>().Save();
#if DEBUG
                    App.Close();
#endif
                });
            }
        }
    }
}
