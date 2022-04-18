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
using Ui.View;

namespace PRM.View
{
    public class MainWindowViewModel : NotifyPropertyChangedBaseScreen, IViewAware
    {
        private readonly IWindowManager _wm;
        public PrmContext Context { get; }
        public ServerListPageViewModel ServerListViewModel { get; } = IoC.Get<ServerListPageViewModel>();
        public SettingsPageViewModel SettingViewModel { get; } = IoC.Get<SettingsPageViewModel>();
        public AboutPageViewModel AboutViewModel { get; } = IoC.Get<AboutPageViewModel>();
        public MainWindowSearchControlViewModel SearchControlViewModel { get; } = IoC.Get<MainWindowSearchControlViewModel>();

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


        public MainWindowViewModel(PrmContext context, IWindowManager wm)
        {
            Context = context;
            _wm = wm;
            ShowList();
        }

        protected override void OnViewLoaded()
        {
            var desktopResolutionWatcher = new DesktopResolutionWatcher();
            desktopResolutionWatcher.OnDesktopResolutionChanged += () =>
            {
                GlobalEventHelper.OnScreenResolutionChanged?.Invoke();
                TaskTrayInit();
            };
            TaskTrayInit();
            
            GlobalEventHelper.ShowProcessingRing += (visibility, msg) =>
            {
                Execute.OnUIThread(() =>
                {
                    if (visibility == Visibility.Visible)
                    {
                        var pvm = IoC.Get<ProcessingRingViewModel>();
                        pvm.ProcessingRingMessage = msg;
                        TopLevelViewModel = pvm;
                    }
                    else
                    {
                        TopLevelViewModel = null;
                    }
                });
            };
            GlobalEventHelper.OnRequestGoToServerEditPage += new GlobalEventHelper.OnRequestGoToServerEditPageDelegate((id, isDuplicate, isInAnimationShow) =>
            {
                if (Context.DataService == null) return;
                if (id <= 0) return;
                Debug.Assert(Context.AppData.VmItemList.Any(x => x.Server.Id == id));
                var server = Context.AppData.VmItemList.First(x => x.Server.Id == id).Server;
                EditorViewModel = new ServerEditorPageViewModel(Context.AppData, Context.DataService, server, isDuplicate);
                ActivateMe();
            });

            GlobalEventHelper.OnGoToServerAddPage += new GlobalEventHelper.OnGoToServerAddPageDelegate((tagNames, isInAnimationShow) =>
            {
                if (Context.DataService == null) return;
                var server = new RDP
                {
                    Tags = tagNames?.Count == 0 ? new List<string>() : new List<string>(tagNames!)
                };
                EditorViewModel = new ServerEditorPageViewModel(Context.AppData, Context.DataService, server);
                ActivateMe();
            });

            GlobalEventHelper.OnRequestGoToServerMultipleEditPage += (servers, isInAnimationShow) =>
            {
                if (Context.DataService == null) return;
                var serverBases = servers as ProtocolBase[] ?? servers.ToArray();
                if (serverBases.Count() > 1)
                    EditorViewModel = new ServerEditorPageViewModel(Context.AppData, Context.DataService, serverBases);
                else
                    EditorViewModel = new ServerEditorPageViewModel(Context.AppData, Context.DataService, serverBases.First());
                ActivateMe();
            };


            if (this.View is Window window)
            {
                var myWindowHandle = new WindowInteropHelper(window).Handle;
                var source = HwndSource.FromHwnd(myWindowHandle);
                source.AddHook(WndProc);
            }

            _wm.ShowWindow(IoC.Get<LauncherWindowViewModel>());
        }

        protected override void OnClose()
        {
            TaskTrayDispose();
            IoC.Get<RemoteWindowPool>().Release();
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
                    ((MainWindowView)this.View).PopupMenu.IsOpen = false;
                }, o => IsShownList());
            }
        }

        #endregion CMD



        #region TaskTray

        private static System.Windows.Forms.NotifyIcon? _taskTrayIcon = null;
        private void TaskTrayInit()
        {
            TaskTrayDispose();
            Debug.Assert(Application.GetResourceStream(ResourceUriHelper.GetUriFromCurrentAssembly("LOGO.ico"))?.Stream != null);
            _taskTrayIcon = new System.Windows.Forms.NotifyIcon
            {
                Text = ConfigurationService.AppName,
                Icon = new System.Drawing.Icon(Application.GetResourceStream(ResourceUriHelper.GetUriFromCurrentAssembly("LOGO.ico")).Stream),
                BalloonTipText = "",
                Visible = true
            };
            ReloadTaskTrayContextMenu();
            GlobalEventHelper.OnLanguageChanged += ReloadTaskTrayContextMenu;
            _taskTrayIcon.MouseDoubleClick += (sender, e) =>
            {
                if (e.Button == System.Windows.Forms.MouseButtons.Left)
                {
                    ActivateMe();
                }
            };
        }

        private void TaskTrayDispose()
        {
            if (_taskTrayIcon != null)
            {
                _taskTrayIcon.Visible = false;
                _taskTrayIcon.Dispose();
                _taskTrayIcon = null;
            }
        }


        private void ReloadTaskTrayContextMenu()
        {
            // rebuild TaskTrayContextMenu while language changed
            if (_taskTrayIcon == null) return;

            var title = new System.Windows.Forms.ToolStripMenuItem(ConfigurationService.AppName);
            title.Click += (sender, args) =>
            {
                HyperlinkHelper.OpenUriBySystem("https://github.com/VShawn/PRemoteM");
            };
            var linkHowToUse = new System.Windows.Forms.ToolStripMenuItem(IoC.Get<ILanguageService>().Translate("about_page_how_to_use"));
            linkHowToUse.Click += (sender, args) =>
            {
                HyperlinkHelper.OpenUriBySystem("https://github.com/VShawn/PRemoteM/wiki");
            };
            var linkFeedback = new System.Windows.Forms.ToolStripMenuItem(IoC.Get<ILanguageService>().Translate("about_page_feedback"));
            linkFeedback.Click += (sender, args) =>
            {
                HyperlinkHelper.OpenUriBySystem("https://github.com/VShawn/PRemoteM/issues");
            };
            var exit = new System.Windows.Forms.ToolStripMenuItem(IoC.Get<ILanguageService>().Translate("Exit"));
            exit.Click += (sender, args) => App.Close();
            _taskTrayIcon.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
            _taskTrayIcon.ContextMenuStrip.Items.Add(title);
            _taskTrayIcon.ContextMenuStrip.Items.Add("-");
            _taskTrayIcon.ContextMenuStrip.Items.Add(linkHowToUse);
            _taskTrayIcon.ContextMenuStrip.Items.Add(linkFeedback);
            _taskTrayIcon.ContextMenuStrip.Items.Add(exit);
        }
        #endregion


        public void ActivateMe(bool isForceActivate = false)
        {
            if (this.View is Window window)
            {
                if (window.WindowState == WindowState.Minimized)
                    window.WindowState = WindowState.Normal;
                if (isForceActivate)
                    HideMe();
                Execute.OnUIThread(() =>
                {
                    window.Visibility = Visibility.Visible;
                    window.Topmost = true;
                    window.ShowInTaskbar = true;
                    window.Activate();
                    window.Topmost = false;
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
                    window.Visibility = Visibility.Hidden;
                });
            }
        }

        public void Exit()
        {
            App.Close();
        }



        /// <summary>
        /// Redirect USB Device
        /// </summary>
        /// <returns></returns>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_DEVICECHANGE = 0x0219;
            if (msg == WM_DEVICECHANGE)
            {
                foreach (var host in IoC.Get<RemoteWindowPool>().ProtocolHosts.Where(x => x.Value is AxMsRdpClient09Host).Select(x => x.Value))
                {
                    if (host is AxMsRdpClient09Host rdp)
                    {
                        SimpleLogHelper.Debug($"rdp.NotifyRedirectDeviceChange((uint){wParam}, (int){lParam})");
                        rdp.NotifyRedirectDeviceChange(msg, (uint)wParam, (int)lParam);
                    }
                }
            }
            return IntPtr.Zero;
        }
    }
}