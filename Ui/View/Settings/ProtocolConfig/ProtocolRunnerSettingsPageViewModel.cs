using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using PRM.Controls;
using PRM.Model.ProtocolRunner;
using PRM.Service;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;

namespace PRM.View.Settings.ProtocolConfig
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
                Runners = new ObservableCollection<Runner>(c.Runners);
                Macros = c.MarcoNames;
                RaisePropertyChanged(nameof(Runners));
                RaisePropertyChanged(nameof(RunnerNames));
                SelectedRunnerName = c.SelectedRunnerName;
                if (Runners.All(x => x.Name != SelectedRunnerName))
                {
                    SelectedRunnerName = c.Runners.FirstOrDefault()?.Name;
                }
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
        public List<string> Macros { get; set; } = new List<string>();

        private RelayCommand _cmdAddRunner;
        public RelayCommand CmdAddRunner
        {
            get
            {
                return _cmdAddRunner ??= new RelayCommand((o) =>
                {
                    var c = _protocolConfigurationService.ProtocolConfigs[_selectedProtocol];
                    // TODO 改为 window manager
                    var name = InputWindow.InputBox(_languageService.Translate("New runner name"), _languageService.Translate("New runner"), validate: new Func<string, string>((str) =>
                     {
                         if (string.IsNullOrWhiteSpace(str))
                             return _languageService.Translate("Can not be empty!");
                         if (c.Runners.Any(x => x.Name == str))
                             return _languageService.Translate("{0} is existed!", str);
                         return "";
                     }), owner: IoC.Get<MainWindowView>()).Trim();
                    if (string.IsNullOrEmpty(name) == false && c.Runners.All(x => x.Name != name))
                    {
                        var newRunner = new ExternalRunner(name) {MarcoNames = c.MarcoNames, ProtocolType = c.ProtocolType};
                        c.Runners.Add(newRunner);
                        Runners.Add(newRunner);
                        RaisePropertyChanged(nameof(RunnerNames));
                    }
                });
            }
        }

        private RelayCommand _cmdDelRunner;
        public RelayCommand CmdDeleteRunner
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

                        if (Runners.All(x => x.Name != SelectedRunnerName))
                        {
                            SelectedRunnerName = c.Runners.FirstOrDefault()?.Name;
                        }
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
                    var c = _protocolConfigurationService.ProtocolConfigs[_selectedProtocol];
                    MessageBox.Show(c.GetAllDescriptions);
                });
            }
        }
    }
}
