using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PRM.Core.Model
{
    public class MatchProviderInfo : NotifyPropertyChangedBase
    {

        private string _name;
        public string Name
        {
            get => _name;
            set => SetAndNotifyIfChanged(nameof(Name), ref _name, value);
        }


        private string _title1 = "";
        public string Title1
        {
            get => _title1;
            set => SetAndNotifyIfChanged(nameof(Title1), ref _title1, value);
        }



        private string _title2 = "";
        public string Title2
        {
            get => _title2;
            set => SetAndNotifyIfChanged(nameof(Title2), ref _title2, value);
        }


        private bool _enabled = true;
        public bool Enabled
        {
            get => _enabled;
            set => SetAndNotifyIfChanged(nameof(Enabled), ref _enabled, value);
        }

        private bool _isEditable = true;
        public bool IsEditable
        {
            get => _isEditable;
            set => SetAndNotifyIfChanged(nameof(IsEditable), ref _isEditable, value);
        }
    }
}
