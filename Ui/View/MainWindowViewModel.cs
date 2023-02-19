using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using _1RM.Model;
using _1RM.Model.DAO;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.Utils;
using _1RM.View.Editor;
using _1RM.View.Settings;
using _1RM.View.Utils;
using Shawn.Utils.Wpf;
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
    public class MainWindowViewModel : MaskLayerContainerScreenBase
    {
        public DataSourceService SourceService { get; }
        public ConfigurationService ConfigurationService { get; }
        public ServerList.ServerListPageViewModel ServerListViewModel { get; } = IoC.Get<ServerList.ServerListPageViewModel>();
        public SettingsPageViewModel SettingViewModel { get; } = IoC.Get<SettingsPageViewModel>();
        public AboutPageViewModel AboutViewModel { get; } = IoC.Get<AboutPageViewModel>();
        private readonly GlobalData _appData;


        #region Properties


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
                    if (_showSetting == true)
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
            ShowList(false);
        }

        public Action? OnMainWindowViewLoaded = null;
        protected override void OnViewLoaded()
        {

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

            GlobalEventHelper.OnRequestGoToServerEditPage += (serverToEdit, isInAnimationShow) =>
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
                var serverBases = servers.Where(x => x.GetDataSource()?.IsWritable == true).ToArray();
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

            //var vm = new _1RM.View.Utils.MessageBoxPageViewModel();
            //vm.Setup(messageBoxText: "content",
            //    caption: "title",
            //    icon: MessageBoxImage.Warning,
            //    buttons: MessageBoxButton.OK,
            //    buttonLabels: new Dictionary<MessageBoxResult, string>()
            //    {
            //        {MessageBoxResult.None, IoC.Get<ILanguageService>().Translate("OK")},
            //        {MessageBoxResult.Yes, IoC.Get<ILanguageService>().Translate("OK")},
            //        {MessageBoxResult.OK, IoC.Get<ILanguageService>().Translate("OK")},
            //    }, onButtonClicked: () =>
            //    {
            //        TopLevelViewModel = null;
            //    });
            //TopLevelViewModel = vm;
        }

        protected override void OnClose()
        {
            App.Close();
        }

        public override void OnShowProcessingRing(long layerId, Visibility visibility, string msg)
        {
            if (this.View is not MainWindowView { IsClosing: false } window) return;
            base.OnShowProcessingRing(layerId, visibility, msg);
        }


        public void ShowList(bool clearSelection)
        {
            if (this.View is not MainWindowView { IsClosing: false } window) return;
            EditorViewModel = null;
            ShowAbout = false;
            ShowSetting = false;
            if (clearSelection)
            {
                ServerListViewModel.ClearSelection();
            }
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


        private RelayCommand? _cmdToggleCardList;
        public RelayCommand CmdToggleCardList
        {
            get
            {
                return _cmdToggleCardList ??= new RelayCommand((o) =>
                {
                    this.ServerListViewModel.ListPageIsCardView = !this.ServerListViewModel.ListPageIsCardView;
                    if (this.View != null)
                        ((MainWindowView)this.View).PopupMenu.IsOpen = false;
                }, o => IsShownList());
            }
        }
        #endregion CMD




        public void ShowMe(bool isForceActivate = false, EnumMainWindowPage? goPage = null)
        {
            if (this.View is not MainWindowView)
            {
                IoC.Get<IWindowManager>().ShowWindow(this);
                return;
            }
            if (this.View is not MainWindowView { IsClosing: false } window) return;

            if (goPage != null)
            {
                switch (goPage)
                {
                    case EnumMainWindowPage.List:
                        ShowList(false);
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

            if (window.Visibility != Visibility.Visible)
            {
                MsAppCenterHelper.TraceView(nameof(MainWindowView), true);
            }
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

        public void HideMe()
        {
            if (this.View is not MainWindowView { IsClosing: false } window) return;
            if (Shawn.Utils.ConsoleManager.HasConsole)
                Shawn.Utils.ConsoleManager.Hide();
            if (window.Visibility == Visibility.Visible)
            {
                MsAppCenterHelper.TraceView(nameof(MainWindowView), false);
            }
            Execute.OnUIThread(() =>
            {
                window.ShowInTaskbar = false;
                window.Hide();
                window.Visibility = Visibility.Hidden;
                // After startup and initalizing our application and when closing our window and minimize the application to tray we free memory with the following line:
                System.Diagnostics.Process.GetCurrentProcess().MinWorkingSet = System.Diagnostics.Process.GetCurrentProcess().MinWorkingSet;
            });
        }

        private RelayCommand? _cmdExit;
        public RelayCommand CmdExit
        {
            get
            {
                return _cmdExit ??= new RelayCommand((o) =>
                {
                    App.Close();
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
                            ServerListViewModel.RefreshCollectionViewSource();
                        }
                    });
                }
            }
        }

        public void SetMainFilterString(List<TagFilter>? tags, List<string>? keywords)
        {
            if (tags?.Count == 1 && tags.First().TagName is ServerList.ServerListPageViewModel.TAB_TAGS_LIST_NAME)
            {
                _mainFilterString = ServerList.ServerListPageViewModel.TAB_TAGS_LIST_NAME;
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