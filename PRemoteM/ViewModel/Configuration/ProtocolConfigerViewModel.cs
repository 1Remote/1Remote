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
            SelectedProtocol = protocolConfigurationService.ProtocolConfigs.First().Key;
        }

        public List<string> Protocols => _protocolConfigurationService.ProtocolConfigs.Keys.ToList();


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
                SelectedRunnerName = c.GetRunner().Name;
            }
        }


        private string _selectedRunnerName;
        public string SelectedRunnerName
        {
            get => _selectedRunnerName;
            set
            {
                SetAndNotifyIfChanged(ref _selectedRunnerName, value);
                var c = _protocolConfigurationService.ProtocolConfigs[_selectedProtocol];
                if (_selectedRunnerName != c.SelectedRunnerName && Runners.Any(x => x.Name == _selectedRunnerName))
                {
                    c.SelectedRunnerName = _selectedRunnerName;
                }
            }
        }
        public List<string> RunnerNames => Runners.Select(x => x.Name).ToList();

        public ObservableCollection<Runner> Runners { get; set; }

        private RelayCommand _cmdAddRunner;
        public RelayCommand CmdAddRunner
        {
            get
            {
                return _cmdAddRunner ??= new RelayCommand((o) =>
                {
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
                        var newRunner = new ExternalRunner(name);
                        c.Runners.Add(newRunner);
                        Runners.Add(newRunner);
                        RaisePropertyChanged(nameof(RunnerNames));
                    }
                });
            }
        }

        private RelayCommand _cmdDelRunner;
        public RelayCommand CmdDelRunner
        {
            get
            {
                return _cmdDelRunner ??= new RelayCommand((o) =>
                {
                    var pn = o.ToString();
                    if (MessageBox.Show(
                        _languageService.Translate("confirm_to_delete"),
                        _languageService.Translate("messagebox_title_warning"),
                        MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.None) == MessageBoxResult.Yes)
                    {
                        var c = _protocolConfigurationService.ProtocolConfigs[_selectedProtocol];
                        if (string.IsNullOrEmpty(pn) == false && c.Runners.Any(x => x.Name == pn))
                        {
                            c.Runners.RemoveAll(x => x.Name == pn);
                        }

                        Runners = new ObservableCollection<Runner>(c.Runners);
                        RaisePropertyChanged(nameof(Runners));
                        RaisePropertyChanged(nameof(RunnerNames));
                        SelectedRunnerName = c.GetRunner().Name;
                        _protocolConfigurationService.Save();
                    }
                });
            }
        }


        private RelayCommand _cmdShowProtocolHelp;
        public RelayCommand CmdShowProtocolHelp
        {
            get
            {
                return _cmdShowProtocolHelp ??= new RelayCommand((o) =>
                {
                    MessageBox.Show(_protocolConfigurationService.ProtocolPropertyDescriptions[_selectedProtocol]);
                });
            }
        }
    }
}
