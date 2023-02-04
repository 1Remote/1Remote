using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using _1RM.Model;
using _1RM.Model.DAO;
using _1RM.Model.DAO.Dapper;
using _1RM.Service;
using _1RM.Utils;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.FileSystem;
using _1RM.Service.DataSource;
using _1RM.View.Settings.DataSource;
using _1RM.View.Settings.General;
using _1RM.View.Settings.Launcher;
using _1RM.View.Settings.ProtocolConfig;
using _1RM.View.Settings.Theme;

namespace _1RM.View.Settings
{
    public partial class SettingsPageViewModel : NotifyPropertyChangedBaseScreen
    {
        private readonly DataSourceService _dataSourceService;
        private ConfigurationService _configurationService => IoC.Get<ConfigurationService>();
        private readonly GlobalData _appData;


        public SettingsPageViewModel(DataSourceService dataSourceService, GlobalData appData)
        {
            _dataSourceService = dataSourceService;
            _appData = appData;
        }

        protected override void OnViewLoaded()
        {
            ShowPage(CurrentPage);
        }


        private INotifyPropertyChanged _selectedViewModel = IoC.Get<GeneralSettingViewModel>();
        public INotifyPropertyChanged SelectedViewModel
        {
            get => _selectedViewModel;
            set => SetAndNotifyIfChanged(ref _selectedViewModel, value);
        }

        public void ShowPage(EnumMainWindowPage page)
        {
            if (page == EnumMainWindowPage.List
                || page == EnumMainWindowPage.About
                || CurrentPage == page)
                return;

            CurrentPage = page;
            switch (page)
            {
                case EnumMainWindowPage.SettingsGeneral:
                    SelectedViewModel = IoC.Get<GeneralSettingViewModel>();
                    break;
                case EnumMainWindowPage.SettingsData:
                    SelectedViewModel = IoC.Get<DataSourceViewModel>();
                    break;
                case EnumMainWindowPage.SettingsLauncher:
                    SelectedViewModel = IoC.Get<LauncherSettingViewModel>();
                    break;
                case EnumMainWindowPage.SettingsTheme:
                    SelectedViewModel = IoC.Get<ThemeSettingViewModel>();
                    break;
                case EnumMainWindowPage.SettingsRunners:
                    SelectedViewModel = IoC.Get<ProtocolRunnerSettingsPageViewModel>();
                    break;
                case EnumMainWindowPage.List:
                case EnumMainWindowPage.About:
                default:
                    return;
            }
        }

        private EnumMainWindowPage _currentPage = EnumMainWindowPage.SettingsGeneral;
        public EnumMainWindowPage CurrentPage
        {
            get => _currentPage;
            private set => SetAndNotifyIfChanged(ref _currentPage, value);
        }

        private Visibility _progressBarVisibility = Visibility.Collapsed;
        public Visibility ProgressBarVisibility
        {
            get => _progressBarVisibility;
            private set => SetAndNotifyIfChanged(ref _progressBarVisibility, value);
        }


        public bool TabHeaderShowCloseButton
        {
            get => _configurationService.General.TabHeaderShowCloseButton;
            set
            {
                if (SetAndNotifyIfChanged(ref _configurationService.General.TabHeaderShowCloseButton, value))
                {
                    _configurationService.Save();
                }
            }
        }

        public bool TabHeaderShowReConnectButton
        {
            get => _configurationService.General.TabHeaderShowReConnectButton;
            set
            {
                if (SetAndNotifyIfChanged(ref _configurationService.General.TabHeaderShowReConnectButton, value))
                {
                    _configurationService.Save();
                }
            }
        }

        private RelayCommand? _cmdSaveAndGoBack;
        public RelayCommand CmdSaveAndGoBack
        {
            get
            {
                return _cmdSaveAndGoBack ??= new RelayCommand((o) =>
                {
                    // check if Db is ok
                    var res = _dataSourceService.LocalDataSource?.Database_SelfCheck() ?? EnumDbStatus.AccessDenied;
                    if (res != EnumDbStatus.OK)
                    {
                        MessageBoxHelper.ErrorAlert(res.GetErrorInfo());
                        return;
                    }

                    if (false == IoC.TryGet<LauncherWindowViewModel>()?.SetHotKey(_configurationService.Launcher.LauncherEnabled,
                            _configurationService.Launcher.HotKeyModifiers, _configurationService.Launcher.HotKeyKey))
                    {
                        return;
                    }

                    // check launcher
                    _configurationService.Save();
                    IoC.Get<ProtocolConfigurationService>().Save();
                    IoC.Get<MainWindowViewModel>().ShowList(false);
                });
            }
        }

        private RelayCommand? _cmdOpenPath;
        public RelayCommand CmdOpenPath
        {
            get
            {
                if (_cmdOpenPath != null) return _cmdOpenPath;
                _cmdOpenPath = new RelayCommand((o) =>
                {
                    var path = o?.ToString() ?? "";
                    if (File.Exists(path))
                    {
                        System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("Explorer.exe")
                        {
                            Arguments = "/e,/select," + path
                        };
                        System.Diagnostics.Process.Start(psi);
                    }

                    if (Directory.Exists(path))
                    {
                        System.Diagnostics.Process.Start("explorer.exe", path);
                    }
                });
                return _cmdOpenPath;
            }
        }



        private RelayCommand? _cmdShowPage;
        public RelayCommand CmdShowPage
        {
            get
            {
                return _cmdShowPage ??= new RelayCommand((o) =>
                {
                    if (o is EnumMainWindowPage page)
                    {
                        ShowPage(page);
                    }
                });
            }
        }





        public bool ListPageIsCardView
        {
            get => _configurationService.General.ListPageIsCardView;
            set
            {
                if (SetAndNotifyIfChanged(ref _configurationService.General.ListPageIsCardView, value))
                {
                    _configurationService.Save();
                }
            }
        }
    }
}
