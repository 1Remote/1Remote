using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using _1RM.Model;
using _1RM.Model.DAO;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.Utils;
using _1RM.View.Editor;
using _1RM.View.Host.ProtocolHosts;
using _1RM.View.Settings;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.Controls;
using Shawn.Utils.Wpf.PageHost;
using Stylet;

namespace _1RM.View
{
    public enum EnumMainWindowPage
    {
        List,
        About,
        SettingsGeneral,
        SettingsData,
        SettingsLauncher,
        SettingsTheme,
        SettingsRunners,
    }
    public class MainWindowViewModel : NotifyPropertyChangedBaseScreen, IViewAware
    {
        public DataSourceService SourceService { get; }
        public ConfigurationService ConfigurationService { get; }
        public ServerListPageViewModel ServerListViewModel { get; } = IoC.Get<ServerListPageViewModel>();
        public SettingsPageViewModel SettingViewModel { get; } = IoC.Get<SettingsPageViewModel>();
        public AboutPageViewModel AboutViewModel { get; } = IoC.Get<AboutPageViewModel>();
        private readonly GlobalData _appData;


        #region Properties


        private INotifyPropertyChanged? _topLevelViewModel;
        public INotifyPropertyChanged? TopLevelViewModel
        {
            get => _topLevelViewModel;
            set => SetAndNotifyIfChanged(ref _topLevelViewModel, value);
        }

        private ServerEditorPageViewModel? _editorViewModel = null;
        public ServerEditorPageViewModel? EditorViewModel
        {
            get => _editorViewModel;
            set => SetAndNotifyIfChanged(ref _editorViewModel, value);
        }

        private bool _showAbout = false;
        public bool ShowAbout
        {
            get => _showAbout;
            set => SetAndNotifyIfChanged(ref _showAbout, value);
        }

        private bool _showSetting = false;
        public bool ShowSetting
        {
            get => _showSetting;
            set
            {
                if (SetAndNotifyIfChanged(ref _showSetting, value))
                {
                    if(_showSetting == true)
                        _appData.StopTick();
                    else
                        _appData.StartTick();
                }
            }
        }

        #endregion Properties

        public MainWindowViewModel(GlobalData appData, DataSourceService sourceService, ConfigurationService configurationService)
        {
            _appData = appData;
            SourceService = sourceService;
            ConfigurationService = configurationService;
            ShowList();
        }

        public Action? OnMainWindowViewLoaded = null;
        protected override void OnViewLoaded()
        {
            GlobalEventHelper.ShowProcessingRing += (visibility, msg) =>
            {
                Execute.OnUIThread(() =>
                {
                    if (visibility == Visibility.Visible)
                    {
                        var pvm = IoC.Get<ProcessingRingViewModel>();
                        pvm.ProcessingRingMessage = msg;
                        this.TopLevelViewModel = pvm;
                    }
                    else
                    {
                        this.TopLevelViewModel = null;
                    }
                });
            };
            GlobalEventHelper.OnRequestGoToServerDuplicatePage += (server, animation) =>
            {
                // select save to which source
                DataSourceBase? source = null;
                if (ConfigurationService.AdditionalDataSource.Any(x => x.Status == EnumDbStatus.OK))
                {
                    var vm = new DataSourceSelectorViewModel();
                    if (IoC.Get<IWindowManager>().ShowDialog(vm, IoC.Get<MainWindowViewModel>()) != true)
                        return;
                    source = SourceService.GetDataSource(vm.SelectedSource.DataSourceName);
                }
                else
                {
                    source = SourceService.LocalDataSource;
                }
                if (source == null) return;
                if (source.IsWritable == false) return;
                EditorViewModel = ServerEditorPageViewModel.Duplicate(_appData, source, server);
                ShowMe();
            };

            GlobalEventHelper.OnRequestGoToServerEditPage += (serverToEdit, isDuplicate, isInAnimationShow) =>
            {
                if (SourceService.LocalDataSource == null) return;
                var server = _appData.VmItemList.FirstOrDefault(x => x.Id == serverToEdit.Id && x.DataSourceName == serverToEdit.DataSourceName)?.Server;
                if (server == null) return;
                if (server.GetDataSource()?.IsWritable != true) return;
                EditorViewModel = ServerEditorPageViewModel.Edit(_appData, server);
                ShowMe();
            };

            GlobalEventHelper.OnGoToServerAddPage += (tagNames, isInAnimationShow) =>
            {
                // select save to which source
                DataSourceBase? source = null;
                if (ConfigurationService.AdditionalDataSource.Any(x => x.Status == EnumDbStatus.OK))
                {
                    var vm = new DataSourceSelectorViewModel();
                    if (IoC.Get<IWindowManager>().ShowDialog(vm) != true)
                        return;
                    source = SourceService.GetDataSource(vm.SelectedSource.DataSourceName);
                }
                else
                {
                    source = SourceService.GetDataSource();
                }
                if (source == null) return;
                if (source.IsWritable == false) return;

                EditorViewModel = ServerEditorPageViewModel.Add(_appData, source, tagNames?.Count == 0 ? new List<string>() : new List<string>(tagNames!));
                ShowMe();
            };

            GlobalEventHelper.OnRequestGoToServerMultipleEditPage += (servers, isInAnimationShow) =>
            {
                var serverBases = servers.Where(x=>x.GetDataSource()?.IsWritable == true).ToArray();
                if (serverBases.Length > 1)
                {
                    EditorViewModel = ServerEditorPageViewModel.BuckEdit(_appData, serverBases);
                }
                else if (serverBases.Length == 1)
                {
                    EditorViewModel = ServerEditorPageViewModel.Edit(_appData, serverBases.First());
                }
                ShowMe();
            };

            OnMainWindowViewLoaded?.Invoke();
        }

        protected override void OnClose()
        {
            App.Close();
        }


        public void ShowList()
        {
            EditorViewModel = null;
            ShowAbout = false;
            ShowSetting = false;
        }

        public bool IsShownList()
        {
            return EditorViewModel is null && ShowAbout == false && ShowSetting == false;
        }


        #region CMD

        private RelayCommand? _cmdGoSysOptionsPage;
        public RelayCommand CmdGoSysOptionsPage
        {
            get
            {
                return _cmdGoSysOptionsPage ??= new RelayCommand((o) =>
                {
                    ShowSetting = true;
                    ShowAbout = false;
                    EditorViewModel = null;
                    if (this.View != null)
                        ((MainWindowView)this.View).PopupMenu.IsOpen = false;
                }, o => IsShownList());
            }
        }

        private RelayCommand? _cmdGoAboutPage;
        public RelayCommand CmdGoAboutPage
        {
            get
            {
                return _cmdGoAboutPage ??= new RelayCommand((o) =>
                {
                    ShowAbout = true;
                    ShowSetting = false;
                    EditorViewModel = null;
                    if (this.View != null)
                        ((MainWindowView)this.View).PopupMenu.IsOpen = false;
                }, o => IsShownList());
            }
        }
        
        #endregion CMD




        public void ShowMe(bool isForceActivate = false, EnumMainWindowPage? goPage = null)
        {
            if (goPage != null)
            {
                switch (goPage)
                {
                    case EnumMainWindowPage.List:
                        ShowList();
                        break;
                    case EnumMainWindowPage.About:
                        CmdGoAboutPage?.Execute();
                        break;
                    case EnumMainWindowPage.SettingsGeneral:
                    case EnumMainWindowPage.SettingsData:
                    case EnumMainWindowPage.SettingsRunners:
                    case EnumMainWindowPage.SettingsLauncher:
                    case EnumMainWindowPage.SettingsTheme:
                        SettingViewModel.ShowPage((EnumMainWindowPage)goPage);
                        CmdGoSysOptionsPage?.Execute();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(goPage), goPage, null);
                }
            }

            if (this.View is Window window)
            {
                Execute.OnUIThread(() =>
                {
                    if (window.WindowState == WindowState.Minimized)
                        window.WindowState = WindowState.Normal;
                    if (isForceActivate)
                        HideMe();
                    window.Show();
                    window.ShowInTaskbar = true;
                    window.Topmost = true;
                    window.Activate();
                    window.Topmost = false;
                    window.Focus();
                });
            }
            else
            {
                Execute.OnUIThread(() =>
                {
                    IoC.Get<IWindowManager>().ShowWindow(this);
                });
            }
        }

        public void HideMe()
        {
            if (Shawn.Utils.ConsoleManager.HasConsole)
                Shawn.Utils.ConsoleManager.Hide();
            if (this.View is Window window)
            {
                Execute.OnUIThread(() =>
                {
                    window.ShowInTaskbar = false;
                    window.Hide();
                    window.Visibility = Visibility.Hidden;
                    // After startup and initalizing our application and when closing our window and minimize the application to tray we free memory with the following line:
                    System.Diagnostics.Process.GetCurrentProcess().MinWorkingSet = System.Diagnostics.Process.GetCurrentProcess().MinWorkingSet;
                });
            }
        }

        private RelayCommand? _cmdExit;
        public RelayCommand CmdExit
        {
            get
            {
                return _cmdExit ??= new RelayCommand((o) =>
                {
                    this.RequestClose();
                });
            }
        }



        #region MainFilter
        private bool _mainFilterIsFocused = false;
        public bool MainFilterIsFocused
        {
            get => _mainFilterIsFocused;
            set => SetAndNotifyIfChanged(ref _mainFilterIsFocused, value);
        }

        private int _mainFilterCaretIndex = 0;
        public int MainFilterCaretIndex
        {
            get => _mainFilterCaretIndex;
            set => SetAndNotifyIfChanged(ref _mainFilterCaretIndex, value);
        }


        private readonly DebounceDispatcher _debounceDispatcher = new();

        private string _mainFilterString = "";
        public string MainFilterString
        {
            get => _mainFilterString;
            set
            {
                // can only be called by the Ui
                if (SetAndNotifyIfChanged(ref _mainFilterString, value))
                {
                    _debounceDispatcher.Debounce(150, (obj) =>
                    {
                        if (_mainFilterString == MainFilterString)
                        {
                            GlobalEventHelper.OnFilterChanged?.Invoke(MainFilterString);
                        }
                    });
                }
            }
        }

        public void SetMainFilterString(List<TagFilter>? tags, List<string>? keywords)
        {
            if (tags?.Count == 1 && tags.First().TagName is ServerListPageViewModel.TAB_TAGS_LIST_NAME)
            {
                _mainFilterString = ServerListPageViewModel.TAB_TAGS_LIST_NAME;
                RaisePropertyChanged(nameof(MainFilterString));
            }
            else
            {
                MainFilterString = TagAndKeywordEncodeHelper.EncodeKeyword(tags, keywords);
                MainFilterCaretIndex = MainFilterString?.Length ?? 0;
            }
        }
        #endregion
    }
}