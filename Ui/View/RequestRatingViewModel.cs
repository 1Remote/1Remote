using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using PRM.Service;
using Shawn.Utils;
using Ui;

namespace PRM.View
{
    public class RequestRatingViewModel : NotifyPropertyChangedBase
    {
        public void Close()
        {
            IoC.Get<MainWindowViewModel>().TopLevelViewModel = null;
            IoC.Get<MainWindowViewModel>().HideMe();
#if DEV
            App.Close();
#endif
        }
    }
}
