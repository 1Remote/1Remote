using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using PRM.Model;
using PRM.Service;
using PRM.Utils;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Shawn.Utils.WpfResources.Theme.Styles;
using Stylet;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBox = System.Windows.Controls.TextBox;

namespace PRM.View
{
    public partial class MainWindowView : WindowChromeBase
    {
        public MainWindowViewModel Vm { get; }
        private readonly IWindowManager _wm;

        public MainWindowView(MainWindowViewModel vm, IWindowManager wm)
        {
            InitializeComponent();
            Vm = vm;
            _wm = wm;
            this.DataContext = Vm;
            Title = ConfigurationService.AppName;
            // restore the window size from 
            this.Width = Vm.Context.LocalityService.MainWindowWidth;
            this.Height = Vm.Context.LocalityService.MainWindowHeight;
            // check the current screen size
            var screenEx = ScreenInfoEx.GetCurrentScreenBySystemPosition(ScreenInfoEx.GetMouseSystemPosition());
            if (this.Width > screenEx.VirtualWorkingArea.Width)
                this.Width = Math.Min(screenEx.VirtualWorkingArea.Width * 0.8, this.Width * 0.8);
            if (this.Height > screenEx.VirtualWorkingArea.Height)
                this.Height = Math.Min(screenEx.VirtualWorkingArea.Height * 0.8, this.Height * 0.8);

            this.SizeChanged += (sender, args) =>
            {
                if (this.WindowState == WindowState.Normal)
                {
                    Vm.Context.LocalityService.MainWindowHeight = this.Height;
                    Vm.Context.LocalityService.MainWindowWidth = this.Width;
                    SimpleLogHelper.Info($"Main window resize to: w = {this.Width}, h = {this.Height}");
                }
            };

            WinTitleBar.PreviewMouseDown += WinTitleBar_OnPreviewMouseDown;
            WinTitleBar.MouseUp += WinTitleBar_OnMouseUp;
            WinTitleBar.PreviewMouseMove += WinTitleBar_OnPreviewMouseMove;

            // Startup Location
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            BtnClose.Click += (sender, args) =>
            {
                if (Vm.AnimationPageEditor == null
                    && (Vm.Context.ConfigurationService.Engagement.DoNotShowAgain == false || AppVersion.VersionData > Vm.Context.ConfigurationService.Engagement.DoNotShowAgainVersion)
                    && Vm.Context.ConfigurationService.Engagement.InstallTime < DateTime.Now.AddDays(-15)
                    && Vm.Context.ConfigurationService.Engagement.LastRequestRatingsTime < DateTime.Now.AddDays(-60)
                    && Vm.Context.ConfigurationService.Engagement.ConnectCount > 100
                   )
                {
                    // 显示“请求应用的评分和评价”页面 https://docs.microsoft.com/zh-cn/windows/uwp/monetize/request-ratings-and-reviews
                    Vm.TopLevelViewModel = IoC.Get<RequestRatingViewModel>();
                    return;
                }

#if DEV
                App.Close();
                return;
#else
                if (Shawn.Utils.ConsoleManager.HasConsole)
                    Shawn.Utils.ConsoleManager.Hide();
                HideMe();
#endif
            };
            this.Closing += (sender, args) =>
            {
                if (this.ShowInTaskbar)
                {
                    HideMe();
                    args.Cancel = true;
#if DEV
                    App.Close();
#endif
                }
                else
                {
                    RemoteWindowPool.Instance?.Release();
                    TaskTrayDispose();
                }
            };
            BtnMaximize.Click += (sender, args) => this.WindowState = (this.WindowState == WindowState.Normal) ? WindowState.Maximized : WindowState.Normal;
            BtnMinimize.Click += (sender, args) => { this.WindowState = WindowState.Minimized; };

            Vm.OnFilterStringChangedByBackend += () =>
            {
                TbFilter.CaretIndex = TbFilter.Text.Length;
            };

            var desktopResolutionWatcher = new DesktopResolutionWatcher();
            this.Loaded += (sender, args) =>
            {
                TaskTrayInit();
                desktopResolutionWatcher.OnDesktopResolutionChanged += () =>
                {
                    GlobalEventHelper.OnScreenResolutionChanged?.Invoke();
                    TaskTrayInit();
                };
                LauncherWindowView = IoC.Get<LauncherWindowView>();
                vm.Init(this);
            };
        }


        public void ActivateMe(bool isForceActivate = false)
        {
            if (this.WindowState == WindowState.Minimized)
                this.WindowState = WindowState.Normal;
            if (isForceActivate)
                HideMe();
            Dispatcher?.Invoke(() =>
            {
                this.Visibility = Visibility.Visible;
                Topmost = true;
                this.ShowInTaskbar = true;
                this.Activate();
                Topmost = false;
            });
        }

        public void HideMe()
        {
            Dispatcher?.Invoke(() =>
            {
                this.ShowInTaskbar = false;
                this.Visibility = Visibility.Hidden;
            });
        }

        public void CloseMe()
        {
            HideMe();
            Close();
        }

        private void CommandFocusFilter_OnExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            TbFilter.Focus();
        }

        private void TbFilter_OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape || !(sender is TextBox textBox)) return;
            var s = TagAndKeywordEncodeHelper.DecodeKeyword(Vm.FilterString);
            Vm.SetFilterStringByBackend(TagAndKeywordEncodeHelper.EncodeKeyword(s.Item1, new List<string>()));
            // Kill logical focus
            FocusManager.SetFocusedElement(FocusManager.GetFocusScope(textBox), null);
            // Kill keyboard focus
            Keyboard.ClearFocus();
            this.Focus();
        }

        private void ButtonExit_OnClick(object sender, RoutedEventArgs e)
        {
            CloseMe();
        }

        private void MainWindow_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.FocusedElement is TextBox)
            {
            }
            else if (e.Key == Key.Escape)
            {
                if (Vm.AnimationPageAbout != null)
                {
                    Vm.AnimationPageAbout = null;
                }
                else if (Vm.AnimationPageSettings != null)
                {
                    Vm.AnimationPageSettings = null;
                }
            }
            else if (e.Key != Key.LeftCtrl && e.Key != Key.RightCtrl)
            {
                TbFilter.Focus();
                TbFilter.CaretIndex = TbFilter.Text.Length;
            }
        }

        private void ProcessingRing_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount >= 2)
                return;
            base.WinTitleBar_OnPreviewMouseDown(sender, e);
        }

        #region TaskTray

        private static System.Windows.Forms.NotifyIcon _taskTrayIcon = null;
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
                    this.ActivateMe();
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

            var title = new System.Windows.Forms.MenuItem(ConfigurationService.AppName);
            title.Click += (sender, args) =>
            {
                System.Diagnostics.Process.Start("https://github.com/VShawn/PRemoteM");
            };
            var @break = new System.Windows.Forms.MenuItem("-");
            var linkHowToUse = new System.Windows.Forms.MenuItem(Vm.Context.LanguageService.Translate("about_page_how_to_use"));
            linkHowToUse.Click += (sender, args) =>
            {
                System.Diagnostics.Process.Start("https://github.com/VShawn/PRemoteM/wiki");
            };
            var linkFeedback = new System.Windows.Forms.MenuItem(Vm.Context.LanguageService.Translate("about_page_feedback"));
            linkFeedback.Click += (sender, args) =>
            {
                System.Diagnostics.Process.Start("https://github.com/VShawn/PRemoteM/issues");
            };
            var exit = new System.Windows.Forms.MenuItem(Vm.Context.LanguageService.Translate("Exit"));
            exit.Click += (sender, args) => App.Close();
            var child = new System.Windows.Forms.MenuItem[] { title, @break, linkHowToUse, linkFeedback, exit };
            _taskTrayIcon.ContextMenu = new System.Windows.Forms.ContextMenu(child);
        }
        #endregion

        public LauncherWindowView LauncherWindowView { get; private set; } = null;

    }
}