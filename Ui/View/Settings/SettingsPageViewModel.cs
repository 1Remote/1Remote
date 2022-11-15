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
            ShowPage(_initPage);
        }


        private INotifyPropertyChanged _selectedViewModel = new DataSourceViewModel();
        public INotifyPropertyChanged SelectedViewModel
        {
            get => _selectedViewModel;
            set => SetAndNotifyIfChanged(ref _selectedViewModel, value);
        }

        private EnumMainWindowPage _initPage = EnumMainWindowPage.SettingsGeneral;
        public void ShowPage(EnumMainWindowPage page)
        {
            if (this.View is SettingsPageView view)
            {
                switch (page)
                {
                    case EnumMainWindowPage.SettingsGeneral:
                        view.TabItemGeneral.IsSelected = true;
                        break;
                    case EnumMainWindowPage.SettingsData:
                        view.TabItemDataBase.IsSelected = true;
                        break;
                    case EnumMainWindowPage.SettingsRunners:
                        view.TabItemRunners.IsSelected = true;
                        break;
                    case EnumMainWindowPage.SettingsLauncher:
                        view.TabItemLauncher.IsSelected = true;
                        break;
                    case EnumMainWindowPage.SettingsTheme:
                        view.TabItemTheme.IsSelected = true;
                        break;
                    case EnumMainWindowPage.List:
                    case EnumMainWindowPage.About:
                    default:
                        CmdSaveAndGoBack.Execute();
                        break;
                }
            }
            else
            {
                _initPage = page;
            }
        }


        private Visibility _progressBarVisibility = Visibility.Collapsed;
        public Visibility ProgressBarVisibility
        {
            get => _progressBarVisibility;
            private set => SetAndNotifyIfChanged(ref _progressBarVisibility, value);
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


                    _configurationService.Save();
                    IoC.Get<ProtocolConfigurationService>().Save();
                    IoC.Get<MainWindowViewModel>().ShowList();
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



        public GeneralSettingViewModel GeneralSettingViewModel => IoC.Get<GeneralSettingViewModel>();
        public LauncherSettingViewModel LauncherSettingViewModel => IoC.Get<LauncherSettingViewModel>();
        public ThemeSettingViewModel ThemeSettingViewModel => IoC.Get<ThemeSettingViewModel>();
        public ProtocolRunnerSettingsPageViewModel ProtocolRunnerSettingsPageViewModel => IoC.Get<ProtocolRunnerSettingsPageViewModel>();



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





        #region UI

        #endregion
    }
}
