using Stylet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using _1RM.Utils;
using Shawn.Utils.Wpf;
using _1RM.Service;
using Shawn.Utils;

namespace _1RM.View.Utils
{
    public enum PingStatus
    {
        None,
        Pinging,
        Canceled,
        Failed,
        Success
    }
    public class PingTestItem : NotifyPropertyChangedBase
    {
        public PingTestItem(string name, string address)
        {
            Name = name;
            Address = address;
        }

        public string Name {get; }
        public string Address {get; }

        private PingStatus _status = PingStatus.None;
        public PingStatus Status
        {
            get => _status;
            set => SetAndNotifyIfChanged(ref _status, value);
        }

        private int _ping = 0;
        /// <summary>
        /// the timespan between start and end of the ping in Milliseconds
        /// </summary>
        public int Ping
        {
            get => _ping;
            set => SetAndNotifyIfChanged(ref _ping, value);
        }
    }

    public class AlternateAddressSwitchingViewModel : NotifyPropertyChangedBaseScreen
    {
        private readonly CancellationTokenSource _cts;
        private string _title = "";

        public AlternateAddressSwitchingViewModel(CancellationTokenSource cts)
        {
            _cts = cts;
        }

        ~AlternateAddressSwitchingViewModel()
        {
            __autoCloseTimer?.Dispose();
        }

        public bool IsCanceled { get; private set; } =false;

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
                    IsCanceled = true;
                    if (_cts.IsCancellationRequested == false) _cts.Cancel();
                    this.RequestClose();
                });
            }
        }

        private System.Timers.Timer? __autoCloseTimer;
        private int _autoCloseEta = 0;
        public void StartAutoCloseCounter(int autoCloseEta = 5)
        {
            _autoCloseEta = autoCloseEta + 3;
            __autoCloseTimer = new System.Timers.Timer(1000);
            __autoCloseTimer.Interval = 1000;
            __autoCloseTimer.Elapsed += (sender, args) =>
            {
                _autoCloseEta--;
                if (_autoCloseEta >= 0 &&
                    (_autoCloseEta < 5 || _autoCloseEta <= autoCloseEta))
                {
                    Eta = _autoCloseEta;
                }

                if (_autoCloseEta <= 0)
                {
                    __autoCloseTimer.AutoReset = false;
                    Execute.OnUIThread(() => this.RequestClose());
                }
            };
            __autoCloseTimer.AutoReset = true;
            __autoCloseTimer.Start();
        }
    }
}
