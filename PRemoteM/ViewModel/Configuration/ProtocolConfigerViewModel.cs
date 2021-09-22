using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using PRM.Controls;
using PRM.Core;
using PRM.Core.Protocol.RDP;
using PRM.Core.Protocol.Runner;
using PRM.Core.Service;
using PRM.View;
using Shawn.Utils;
using Shawn.Utils.PageHost;

namespace PRM.ViewModel.Configuration
{
    public class ProtocolConfigerViewModel : NotifyPropertyChangedBase
    {
        private readonly ProtocolConfigurationService _protocolConfigurationService;

        public ProtocolConfigerViewModel(ProtocolConfigurationService protocolConfigurationService)
        {
            _protocolConfigurationService = protocolConfigurationService;
            SelectedProtocol = ProtocolServerRDP.ProtocolName;
        }

        public List<string> Protocols
        {
            get => _protocolConfigurationService.ProtocolConfigs.Keys.ToList();
        }


        private string _selectedProtocol = "";
        public string SelectedProtocol
        {
            get => _selectedProtocol;
            set
            {
                SetAndNotifyIfChanged(ref _selectedProtocol, value);
                var c = _protocolConfigurationService.ProtocolConfigs[_selectedProtocol];
                Runners = c.Runners;
                SelectedRunner = c.GetRunner();
                RaisePropertyChanged(nameof(Runners));
                RaisePropertyChanged(nameof(SelectedRunner));
            }
        }

        private Runner _selectedRunner;
        public Runner SelectedRunner
        {
            get => _selectedRunner;
            set => SetAndNotifyIfChanged(ref _selectedRunner, value);
        }

        public List<Runner> Runners { get; set; }

        private RelayCommand _cmdAddProtocol;

        public RelayCommand CmdAddProtocol
        {
            get
            {
                return _cmdAddProtocol ??= new RelayCommand((o) =>
                {
                    // TODO 弹窗输入新的协议名称，并校验
                    var name = InputWindow.InputBox("New protocol name:", "New protocol");
                    if (string.IsNullOrEmpty(name) == false)
                    {
                        MessageBox.Show(name);
                        RaisePropertyChanged(nameof(Protocols));
                    }
                });
            }
        }
    }
}
