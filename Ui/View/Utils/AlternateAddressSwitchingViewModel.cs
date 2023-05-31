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
using Shawn.Utils;

namespace _1RM.View.Utils
{
    public class PingTestItem : NotifyPropertyChangedBase
    {
        public enum PingStatus
        {
            None,
            Testing,
            Failed,
            Success
        }


        public PingTestItem(string name)
        {
            Name = name;
        }

        public string Name {get; }

        private PingStatus _status = PingStatus.None;
        public PingStatus Status
        {
            get => _status;
            set => SetAndNotifyIfChanged(ref _status, value);
        }

        private int _ping = 0;
        public int Ping
        {
            get => _ping;
            set => SetAndNotifyIfChanged(ref _ping, value);
        }
    }

    public class AlternateAddressSwitchingViewModel : NotifyPropertyChangedBaseScreen
    {
        private string _title = "";
        public string Title
        {
            get => _title;
            set => SetAndNotifyIfChanged(ref _title, value);
        }

        private int _eta = 0;
        public int Eta
        {
            get => _eta;
            set => SetAndNotifyIfChanged(ref _eta, value);
        }

        private List<PingTestItem> _pingTestItems = new List<PingTestItem>();
        public List<PingTestItem> PingTestItems
        {
            get => _pingTestItems;
            set => SetAndNotifyIfChanged(ref _pingTestItems, value);
        } 


        private RelayCommand? _cmdCloseContinue;
        public RelayCommand CmdCloseContinue
        {
            get
            {
                return _cmdCloseContinue ??= new RelayCommand((o) =>
                {
                    this.RequestClose();
                });
            }
        }

        private RelayCommand? _cmdCloseEnd;

        public RelayCommand CmdCloseEnd
        {
            get
            {
                return _cmdCloseEnd ??= new RelayCommand((o) =>
                {
                    // TODO confirm
                    this.RequestClose();
                });
            }
        }
    }
}
