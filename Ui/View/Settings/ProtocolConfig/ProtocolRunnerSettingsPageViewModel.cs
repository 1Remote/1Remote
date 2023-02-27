using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using _1RM.Controls;
using _1RM.Model.Protocol;
using _1RM.Model.ProtocolRunner;
using _1RM.Service;
using _1RM.Utils;
using _1RM.View.Utils;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;

namespace _1RM.View.Settings.ProtocolConfig
{
    public class ProtocolRunnerSettingsPageViewModel : NotifyPropertyChangedBase
    {
        private readonly ProtocolConfigurationService _protocolConfigurationService;
        private readonly ILanguageService _languageService;

        public ProtocolRunnerSettingsPageViewModel(ProtocolConfigurationService protocolConfigurationService, ILanguageService languageService)
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
                var selectedRunner = c.SelectedRunnerName;
                Runners.Clear();
                foreach (var runner in c.Runners)
                {
                    Runners.Add(runner);
                }
                if (Runners.Count > 0 && Runners.All(x => x.Name != selectedRunner))
                {
                    SelectedRunner = Runners.First();
                }
                else if(Runners.Count > 0)
                {
                    SelectedRunner = Runners.First(x => x.Name == selectedRunner);
                }
            }
        }


        private Runner? _selectedRunner;
        public Runner? SelectedRunner
        {
            get
            {
                if (Runners.Count > 0 && Runners.All(x => x != _selectedRunner))
                {
                    _selectedRunner = Runners.First();
                }
                return _selectedRunner;
            }
            set
            {
                var nv = value;
                if (nv != null && Runners.Count > 0 && Runners.Any(x => x == value))
                {
                    var c = _protocolConfigurationService.ProtocolConfigs[_selectedProtocol];
                    c.SelectedRunnerName = nv.Name;
                }
                else if (Runners.Count > 0 && Runners.All(x => x != value))
                {
                    nv = Runners.First();
                }
                SetAndNotifyIfChanged(ref _selectedRunner, nv);
            }
        }

        public ObservableCollection<Runner> Runners { get; } = new ObservableCollection<Runner>();

        private RelayCommand? _cmdAddRunner;
        public RelayCommand CmdAddRunner
        {
            get
            {
                return _cmdAddRunner ??= new RelayCommand((o) =>
                {
                    var c = _protocolConfigurationService.ProtocolConfigs[_selectedProtocol];
                    var name = InputBoxViewModel.GetValue(_languageService.Translate("New runner name"), new Func<string, string>((str) =>
                    {
                        if (string.IsNullOrWhiteSpace(str))
                            return _languageService.Translate("Can not be empty!");
                        if (c.Runners.Any(x => x.Name == str))
                            return _languageService.Translate("XXX is already existed!", str);
                        return "";
                    }), ownerViewModel: IoC.Get<MainWindowViewModel>());

                    if (string.IsNullOrEmpty(name) == false && c.Runners.All(x => x.Name != name))
                    {
                        var newRunner = new ExternalRunner(name, SelectedProtocol) { MarcoNames = c.MarcoNames };
                        if (SelectedProtocol == SSH.ProtocolName || SelectedProtocol == SFTP.ProtocolName)
                        {
                            newRunner = new ExternalRunnerForSSH(name, SelectedProtocol) { MarcoNames = c.MarcoNames };
                        }
                        c.Runners.Add(newRunner);
                        Runners.Add(newRunner);
                    }
                });
            }
        }

        private RelayCommand? _cmdDelRunner;
        public RelayCommand CmdDeleteRunner
        {
            get
            {
                return _cmdDelRunner ??= new RelayCommand((o) =>
                {
                    var pn = o?.ToString();
                    if (pn == null) return;

                    if (true == MessageBoxHelper.Confirm(IoC.Get<ILanguageService>().Translate("confirm_to_delete"), ownerViewModel: IoC.Get<MainWindowViewModel>()))
                    {
                        var c = _protocolConfigurationService.ProtocolConfigs[_selectedProtocol];
                        if (string.IsNullOrEmpty(pn) == false && c.Runners.Any(x => x.Name == pn))
                        {
                            c.Runners.RemoveAll(x => x.Name == pn);
                        }
                        Runners.Clear();
                        foreach (var runner in c.Runners)
                        {
                            Runners.Add(runner);
                        }
                        if (Runners.All(x => x.Name != SelectedRunner?.Name))
                        {
                            SelectedRunner = c.Runners.First();
                        }
                        _protocolConfigurationService.Save();
                    }
                });
            }
        }


        private RelayCommand? _cmdShowProtocolHelp;
        public RelayCommand CmdShowProtocolHelp
        {
            get
            {
                return _cmdShowProtocolHelp ??= new RelayCommand((o) =>
                {
                    var c = _protocolConfigurationService.ProtocolConfigs[_selectedProtocol];
                    MessageBoxHelper.Info(c.GetAllDescriptions);
                });
            }
        }
    }
}
