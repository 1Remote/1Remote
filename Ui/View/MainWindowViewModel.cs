using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using _1RM.Model;
using _1RM.Service.DataSource.DAO;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Utils;
using _1RM.View.Editor;
using _1RM.View.Settings;
using _1RM.View.Utils;
using Shawn.Utils.Wpf;
using Stylet;
using _1RM.View.Settings.General;
using SetSelfStartingHelper = _1RM.Utils.SetSelfStartingHelper;
using Shawn.Utils;

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
            set
            {
                SetAndNotifyIfChanged(ref _editorViewModel, value);
                RaisePropertyChanged(nameof(IsShownList));
            }
        }

        //private bool _showAbout = false;
        //public bool ShowAbout
        //{
        //    get => _showAbout;
        //    set
        //    {
        //        SetAndNotifyIfChanged(ref _showAbout, value);
        //        RaisePropertyChanged(nameof(IsShownList));
        //    }
        //}

        private bool _showSetting = false;
        public bool ShowSetting
        {
            get => _showSetting;
            set
            {
                if (SetAndNotifyIfChanged(ref _showSetting, value))
                {
                    RaisePropertyChanged(nameof(IsShownList));
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
            base.OnViewLoaded();

            GlobalEventHelper.OnGoToServerAddPage += async void (tagNames, assignDataSource) =>
            {
                try
                {
                    var source = await DataSourceSelectorViewModel.SelectDataSourceAsync();
#if !DEBUG
                    // use this to test the error message
                    if (source == null)
                    {
                        return;
                    }
#endif
                    if (source?.IsWritable == true)
                    {
                        EditorViewModel = ServerEditorPageViewModel.Add(_appData, source, tagNames?.Count == 0 ? new List<string>() : new List<string>(tagNames!));
                        ShowMe();
                    }
                    else
                    {
                        MessageBoxHelper.ErrorAlert($"Can not add server to DataSource ({source?.DataSourceName ?? "null"}) since it is not writable.");
                    }
                }
                catch (Exception e)
                {
                    SimpleLogHelper.Error(e);
                    SentryIoHelper.Error(e, new Dictionary<string, string>()
                    {
                        {"Action", "MainWindowViewModel.OnGoToServerAddPage"}
                    });
                }
            };
            GlobalEventHelper.OnRequestGoToServerDuplicatePage += async void (server, isInAnimationShow) =>
            {
                try
                {
                    var source = await DataSourceSelectorViewModel.SelectDataSourceAsync();
                    if (source == null)
                    {
                        return;
                    }
                    if (source.IsWritable == true)
                    {
                        EditorViewModel = ServerEditorPageViewModel.Duplicate(_appData, source, server);
                        ShowMe();
                    }
                    else
                    {
                        MessageBoxHelper.ErrorAlert($"Can not add server to DataSource ({source?.DataSourceName ?? "null"}) since it is not writable.");
                    }
                }
                catch (Exception e)
                {
                    SimpleLogHelper.Error(e);
                    SentryIoHelper.Error(e, new Dictionary<string, string>()
                    {
                        {"Action", "MainWindowViewModel.OnRequestGoToServerDuplicatePage"}
                    });
                }
            };

            GlobalEventHelper.OnRequestGoToServerEditPage += (serverToEdit, isInAnimationShow) =>
            {
                var server = _appData.VmItemList.FirstOrDefault(x => x.Id == serverToEdit.Id && x.DataSource == serverToEdit.DataSource)?.Server;
                if (server is not { DataSource: { IsWritable: true } })
                {
                    MessageBoxHelper.ErrorAlert("Can not edit server since it is not writable.");
                }
                else
                {
                    EditorViewModel = ServerEditorPageViewModel.Edit(_appData, server);
                    ShowMe();
                }
            };

            GlobalEventHelper.OnRequestGoToServerMultipleEditPage += (servers, isInAnimationShow) =>
            {
                var serverBases = servers.Where(x => x.DataSource is { IsWritable: true, Status: EnumDatabaseStatus.OK }).ToArray();
                EditorViewModel = null;
                EditorViewModel = serverBases.Length switch
                {
                    > 1 => ServerEditorPageViewModel.BuckEdit(_appData, serverBases),
                    1 => ServerEditorPageViewModel.Edit(_appData, serverBases.First()),
                    _ => EditorViewModel
                };
                if (EditorViewModel == null)
                {
                    MessageBoxHelper.ErrorAlert("Can not edit multiple servers since they are all not writable.");
                    return;
                }
                ShowMe();
            };
#if FOR_MICROSOFT_STORE_ONLY
            SetSelfStartingHelper.SetSelfStartByStartupTask(Assert.APP_NAME, null);
#endif
            OnMainWindowViewLoaded?.Invoke();

            //var vm = new _1RM.View.Utils.MessageBoxPageViewModel();
            //vm.Setup(messageBoxText: "content",
            //    caption: "title",s
            //    icon: MessageBoxImage.Warning,
            //    buttons: MessageBoxButton.OK,
            //    buttonLabels: new Dictionary<MessageBoxResult, string>()
            //    {
            //        {MessageBoxResult.None, IoC.Translate("OK")},
            //        {MessageBoxResult.Yes, IoC.Translate("OK")},
            //        {MessageBoxResult.OK, IoC.Translate("OK")},
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
            ShowSetting = false;
            if (clearSelection)
            {
                ServerListViewModel.ClearSelection();
            }
        }


        public bool IsShownList => EditorViewModel is null && ShowSetting == false;


        #region CMD

        private RelayCommand? _cmdGoSysOptionsPage;
        public RelayCommand CmdGoSysOptionsPage
        {
            get
            {
                return _cmdGoSysOptionsPage ??= new RelayCommand((o) =>
                {
                    ShowSetting = true;
                    EditorViewModel = null;
                    IoC.Get<GeneralSettingViewModel>().AppStartAutomatically = SetSelfStartingHelper.IsSelfStart(Assert.APP_NAME);
                    if (this.View is MainWindowView v)
                        v.PopupMenu.IsOpen = false;
                }, o => IsShownList);
            }
        }

        private RelayCommand? _cmdGoAboutPage;
        public RelayCommand CmdGoAboutPage
        {
            get
            {
                return _cmdGoAboutPage ??= new RelayCommand((o) =>
                {
                    if (this.View is MainWindowView v)
                        v.PopupMenu.IsOpen = false;
                    MaskLayerController.ShowWindowWithMask(AboutViewModel, this);
                });
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
                    if (this.View is MainWindowView v)
                        v.PopupMenu.IsOpen = false;
                }, o => IsShownList);
            }
        }

        private RelayCommand? _cmdReOrder;
        public RelayCommand CmdReOrder
        {
            get
            {
                return _cmdReOrder ??= new RelayCommand((o) =>
                {
                    ServerListViewModel.CmdReOrder.Execute(o);
                    if (this.View is MainWindowView v)
                        v.PopupMenu.IsOpen = false;
                }, o => IsShownList);
            }
        }

        #endregion CMD




        public void ShowMe(bool isForceActivate = false, EnumMainWindowPage? goPage = null)
        {
            if (this.View is not MainWindowView)
            {
                Execute.OnUIThreadSync(() =>
                {
                    IoC.Get<IWindowManager>().ShowWindow(this);
                });
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

            Execute.OnUIThread(() =>
            {
                if (window.IsVisible) return;
                lock (window)
                {
                    if (window.WindowState == WindowState.Minimized)
                        window.WindowState = WindowState.Normal;
                    if (window.IsVisible) return;
                    if (isForceActivate) HideMe();
                    window.Show();
                    window.ShowInTaskbar = true;
                    window.Topmost = true;
                    window.Activate();
                    window.Topmost = false;
                    window.Focus();
                }
            });
        }

        public void HideMe()
        {
            if (this.View is not MainWindowView { IsClosing: false } window) return;
            if (Shawn.Utils.ConsoleManager.HasConsole)
                Shawn.Utils.ConsoleManager.Hide();
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
                    _debounceDispatcher.Debounce(IoC.Get<GlobalData>().VmItemList.Count > 100 ? 200 : 100, (obj) =>
                    {
                        if (_mainFilterString == MainFilterString)
                        {
                            // MainFilterString changed -> refresh view source -> calc visible in `ServerListItemSource_OnFilter`
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