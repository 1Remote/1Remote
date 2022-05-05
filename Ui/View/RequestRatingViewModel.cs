using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using PRM.Service;
using PRM.Utils;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Ui;

namespace PRM.View
{
    public class RequestRatingViewModel : NotifyPropertyChangedBase
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
#if DEV
                    App.Close();
#endif
                });
            }
        }
    }
}
