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

namespace _1RM.View.Utils
{
    /// <summary>
    /// Default implementation of IMessageBoxViewModel, and is therefore the ViewModel shown by default by ShowMessageBox
    /// </summary>
    public class WaitingViewModel : NotifyPropertyChangedBaseScreen
    {
        private string _title = "";
        public string Title
        {
            get => _title;
            set => SetAndNotifyIfChanged(ref _title, value);
        }
        private string _message = "";
        public string Message
        {
            get => _message;
            set => SetAndNotifyIfChanged(ref _message, value);
        }
    }
}
