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
        private readonly LanguageService _languageService;

        public ProtocolConfigerViewModel(ProtocolConfigurationService protocolConfigurationService, LanguageService languageService)
        {
            _protocolConfigurationService = protocolConfigurationService;
            _languageService = languageService;
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
                Runners = new ObservableCollection<Runner>(c.Runners);
                RaisePropertyChanged(nameof(Runners));
                RaisePropertyChanged(nameof(RunnerNames));
                RaisePropertyChanged(nameof(SelectedRunner));
                RaisePropertyChanged(nameof(SelectedRunnerName));
            }
        }


        public Runner SelectedRunner
        {
            get
            {
                var c = _protocolConfigurationService.ProtocolConfigs[_selectedProtocol];
                return c.GetRunner();
            }
        }

        public List<string> RunnerNames => Runners.Select(x => x.Name).ToList();
        public string SelectedRunnerName
        {
            get => SelectedRunner.Name;
            set
            {
                var newName = value.Trim();
                if (newName != SelectedRunnerName && Runners.Any(x => x.Name == newName))
                {
                    var c = _protocolConfigurationService.ProtocolConfigs[_selectedProtocol];
                    c.SelectedRunnerName = newName;
                }
                RaisePropertyChanged(nameof(SelectedRunner));
                RaisePropertyChanged(nameof(SelectedRunnerName));
            }
        }

        public ObservableCollection<Runner> Runners { get; set; }

        private RelayCommand _cmdAddProtocol;

        public RelayCommand CmdAddProtocol
        {
            get
            {
                return _cmdAddProtocol ??= new RelayCommand((o) =>
                {
                    // TODO 弹窗输入新的协议名称，并校验
                    var c = _protocolConfigurationService.ProtocolConfigs[_selectedProtocol];
                    var name = InputWindow.InputBox("New protocol name:", "New protocol", validate: new Func<string, string>((str) =>
                     {
                         if (string.IsNullOrWhiteSpace(str))
                             return _languageService.Translate("Can not be empty!");
                         if (c.Runners.Any(x => x.Name == str))
                             return _languageService.Translate("{0} is existed!", str);
                         return "";
                     })).Trim();
                    if (string.IsNullOrEmpty(name) == false && c.Runners.All(x => x.Name != name))
                    {
                        RaisePropertyChanged(nameof(Protocols));
                        var newRunner = new ExternRunner(name, c.ProtocolName);
                        c.Runners.Add(newRunner);
                        Runners.Add(newRunner);
                    }
                });
            }
        }
    }
}
