using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using PRM.Model;
using PRM.Model.Protocol;
using PRM.Model.Protocol.Base;
using PRM.Service;
using PRM.Utils;
using PRM.View.Editor;
using PRM.View.Host.ProtocolHosts;
using PRM.View.Settings;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.Controls;
using Shawn.Utils.Wpf.PageHost;
using Stylet;
using Ui;

namespace PRM.View
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
        private readonly IWindowManager _wm;
        public PrmContext Context { get; }
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
            set => SetAndNotifyIfChanged(ref _showSetting, value);
        }




        #endregion Properties


        public MainWindowViewModel(PrmContext context, IWindowManager wm, GlobalData appData)
        {
            Context = context;
            _wm = wm;
            _appData = appData;
            ShowList();
        }


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
            GlobalEventHelper.OnRequestGoToServerEditPage += new GlobalEventHelper.OnRequestGoToServerEditPageDelegate((id, isDuplicate, isInAnimationShow) =>
            {
                if (Context.DataService == null) return;
                if (id <= 0) return;
                Debug.Assert(_appData.VmItemList.Any(x => x.Server.Id == id));
                var server = _appData.VmItemList.First(x => x.Server.Id == id).Server;
                EditorViewModel = new ServerEditorPageViewModel(_appData, Context.DataService, server, isDuplicate);
                ShowMe();
            });

            GlobalEventHelper.OnGoToServerAddPage += new GlobalEventHelper.OnGoToServerAddPageDelegate((tagNames, isInAnimationShow) =>
            {
                if (Context.DataService == null) return;
                var server = new RDP
                {
                    Tags = tagNames?.Count == 0 ? new List<string>() : new List<string>(tagNames!)
                };
                EditorViewModel = new ServerEditorPageViewModel(_appData, Context.DataService, server);
                ShowMe();
            });

            GlobalEventHelper.OnRequestGoToServerMultipleEditPage += (servers, isInAnimationShow) =>
            {
                if (Context.DataService == null) return;
                var serverBases = servers as ProtocolBase[] ?? servers.ToArray();
                if (serverBases.Length > 1)
                    EditorViewModel = new ServerEditorPageViewModel(_appData, Context.DataService, serverBases);
                else
                    EditorViewModel = new ServerEditorPageViewModel(_appData, Context.DataService, serverBases.First());
                ShowMe();
            };

            if (this.View is Window window)
            {
                var myWindowHandle = new WindowInteropHelper(window).Handle;
                var source = HwndSource.FromHwnd(myWindowHandle);
                source.AddHook(WndProc);
            }
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
                if (window.WindowState == WindowState.Minimized)
                    window.WindowState = WindowState.Normal;
                if (isForceActivate)
                    HideMe();
                if (window.ShowInTaskbar == false)
                {
                    Execute.OnUIThread(() =>
                    {
                        window.ShowInTaskbar = true;
                        window.Topmost = true;
                        window.Activate();
                        window.Topmost = false;
                        window.Focus();
                    });
                }
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

        /// <summary>
        /// Redirect USB Device
        /// </summary>
        /// <returns></returns>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_DEVICECHANGE = 0x0219;
            try
            {
                if (msg == WM_DEVICECHANGE)
                {
                    foreach (var host in IoC.Get<SessionControlService>().ConnectionId2Hosts.Where(x => x.Value is AxMsRdpClient09Host).Select(x => x.Value))
                    {
                        if (host is AxMsRdpClient09Host rdp)
                        {
                            SimpleLogHelper.Debug($"rdp.NotifyRedirectDeviceChange((uint){wParam}, (int){lParam})");
                            rdp.NotifyRedirectDeviceChange(msg, (uint)wParam, (int)lParam);
                        }
                    }
                }
            }
            finally
            {
            }
            return IntPtr.Zero;
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

        private string _mainFilterString = "";
        public string MainFilterString
        {
            get => _mainFilterString;
            set
            {
                // can only be called by the Ui
                if (SetAndNotifyIfChanged(ref _mainFilterString, value))
                {
                    Task.Factory.StartNew(() =>
                    {
                        var filter = MainFilterString;
                        Thread.Sleep(100);
                        if (filter == MainFilterString)
                        {
                            GlobalEventHelper.OnFilterChanged?.Invoke(MainFilterString);
                        }
                    });
                }
            }
        }

        public void SetMainFilterString(List<TagFilter>? tags, List<string>? keywords, bool InvokeOnFilterChanged = true)
        {
            if (tags?.Count == 1 && tags.First().TagName is ServerListPageViewModel.TAB_TAGS_LIST_NAME)
            {
                _mainFilterString = "";
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