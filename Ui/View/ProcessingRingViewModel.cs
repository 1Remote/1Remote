using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shawn.Utils;

namespace _1RM.View
{
    public class ProcessingRingViewModel : NotifyPropertyChangedBase
    {
        private string _processingRingMessage = "";
        public string ProcessingRingMessage
        {
            get => _processingRingMessage;
            set => SetAndNotifyIfChanged(ref _processingRingMessage, value);
        }
    }
}
