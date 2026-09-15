using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using _1RM.Service;
using _1RM.Service.Locality;
using _1RM.Utils;
using _1RM.View.Utils;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Shawn.Utils.WpfResources.Theme.Styles;
using Stylet;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBox = System.Windows.Controls.TextBox;

namespace _1RM.View
{
    public partial class MainWindowView : WindowChromeBase
    {
        public MainWindowViewModel Vm { get; }
        private readonly ConfigurationService _configurationService;


        public MainWindowView(MainWindowViewModel vm, LocalityService localityService, ConfigurationService configurationService)
        {
            InitializeComponent();
            Vm = vm;
            _configurationService = configurationService;
            this.DataContext = Vm;
            Title = Assert.APP_DISPLAY_NAME;
            // restore the window size from 
            this.Width = localityService.MainWindowWidth;
            this.Height = localityService.MainWindowHeight;
            this.WindowState = localityService.MainWindowState;

            this.SizeChanged += (sender, args) =>
            {
                if (this.WindowState == WindowState.Normal)
                {
                    localityService.MainWindowState = this.WindowState;
                    localityService.MainWindowHeight = this.Height;
                    localityService.MainWindowWidth = this.Width;
                    SimpleLogHelper.Debug($"Main window resize to: w = {this.Width}, h = {this.Height}");
                }
            };

            this.LocationChanged += (sender, args) =>
            {
                localityService.MainWindowTop = this.Top;
                localityService.MainWindowLeft = this.Left;
                SimpleLogHelper.Debug($"Main window move to: top = {this.Top}, left = {this.Left}");
            };

            this.StateChanged += (sender, args) =>
            {
                localityService.MainWindowState = this.WindowState;
            };

            WinTitleBar.PreviewMouseDown += WinTitleBar_OnPreviewMouseDown;
            WinTitleBar.PreviewMouseMove += WinTitleBar_OnPreviewMouseMove;

            // 按配置应用界面引擎（WPF / WebView2 壳）
            this.Loaded += MainWindowView_OnLoaded;

            // WebView2 用户数据目录固定到本机数据目录（便携模式随程序目录、AppData 模式随本地应用数据），
            // 避免默认落在 exe 所在目录污染安装目录；浏览器 profile 属于本机数据，不参与同步
            WebUI.CreationProperties = new Microsoft.Web.WebView2.Wpf.CoreWebView2CreationProperties
            {
                UserDataFolder = System.IO.Path.Combine(AppPathHelper.Instance.LocalityDirPath, "WebView2"),
            };

            // WebView2 初始化是异步的：运行时缺失(LTSC/Server)或用户数据目录不可写等失败
            // 通过此事件异步上报（Core 包装的参数为 IsSuccess/InitializationException），
            // 不处理会进入全局未处理异常导致应用退出
            WebUI.CoreWebView2InitializationCompleted += (_, e) =>
            {
                if (e.IsSuccess == false)
                {
                    SimpleLogHelper.Error(e.InitializationException);
                    WebUI.Visibility = Visibility.Collapsed;
                }
            };

            // WebView2 是 HwndHost（airspace）：其 HWND 恒渲染在本窗口所有 WPF 内容之上，
            // XAML 层级对它无效。因此 TopLevel 遮罩（等待动画/弹窗等）显示期间必须隐藏 WebUI，
            // 遮罩关闭后再按配置恢复（已初始化的 WebView2 隐藏/显示不丢失页面状态）
            Vm.PropertyChanged += VmOnPropertyChanged;

            // Restore or reset window location
            if (double.IsNaN(localityService.MainWindowTop) || double.IsNaN(localityService.MainWindowLeft)
                || localityService.MainWindowTop < SystemParameters.VirtualScreenTop
                || localityService.MainWindowTop + this.Height > SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight
                || localityService.MainWindowLeft < SystemParameters.VirtualScreenLeft
                || localityService.MainWindowLeft + this.Width > SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth)
            {
                ResetLocation();
            }
            else
            {
                this.Top = localityService.MainWindowTop;
                this.Left = localityService.MainWindowLeft;
            }

            BtnClose.Click += (sender, args) =>
            {
                if ((_configurationService.Engagement.DoNotShowAgain == false || AppVersion.VersionData > _configurationService.Engagement.DoNotShowAgainVersion)
                    && _configurationService.Engagement.InstallTime < DateTime.Now.AddDays(-15)
                    && _configurationService.Engagement.LastRequestRatingsTime < DateTime.Now.AddDays(-60)
                    && _configurationService.Engagement.ConnectCount > 100
                   )
                {
                    // 显示“请求应用的评分和评价”页面 https://docs.microsoft.com/zh-cn/windows/uwp/monetize/request-ratings-and-reviews
                    MaskLayerController.ShowMask(IoC.Get<RequestRatingViewModel>(), Vm);
                    return;
                }
                vm.HideMe();
#if DEBUG
                App.Close();
#else
                switch (IoC.Get<ConfigurationService>().General.CloseButtonBehavior)
                {
                    case (int)GeneralConfig.EnumCloseButtonBehavior.Exit:
                        App.Close();
                        break;
                    case (int)GeneralConfig.EnumCloseButtonBehavior.Minimize:
                        // Minimize to system tray - just hide
                    default:
                        break;
                }
#endif
            };

            BtnMaximize.Click += (sender, args) => this.WindowState = (this.WindowState == WindowState.Normal) ? WindowState.Maximized : WindowState.Normal;
            BtnMinimize.Click += (sender, args) => { this.WindowState = WindowState.Minimized; };
        }

        public void ResetLocation()
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            var screenEx = ScreenInfoEx.GetCurrentScreenBySystemPosition(ScreenInfoEx.GetMouseSystemPosition());
            // Check the current screen size
            this.WindowState = WindowState.Normal;
            if (this.Width > screenEx.VirtualWorkingArea.Width)
                this.Width = Math.Min(screenEx.VirtualWorkingArea.Width * 0.8, this.Width * 0.8);
            if (this.Height > screenEx.VirtualWorkingArea.Height)
                this.Height = Math.Min(screenEx.VirtualWorkingArea.Height * 0.8, this.Height * 0.8);
            // Place the window in the center of the current screen
            this.Top = screenEx.VirtualWorkingAreaCenter.Y - this.Height / 2;
            this.Left = screenEx.VirtualWorkingAreaCenter.X - this.Width / 2;
        }


        private void MainWindowView_OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyUiEngineFromConfig();
        }

        private void VmOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(Vm.TopLevelViewModel))
                return;
            // TopLevel 遮罩出现时隐藏 WebUI（HwndHost airspace，见构造函数注释）；关闭后按配置恢复。
            // PropertyChanged 可能在工作线程触发（多处 mask 开关来自后台任务），DP 写入必须回 UI 线程
            Execute.OnUIThread(() =>
            {
                if (Vm.TopLevelViewModel != null)
                {
                    WebUI.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ApplyUiEngineFromConfig();
                }
            });
        }

        /// <summary>
        /// 读取 GeneralConfig.UiEngine 并切换界面引擎（在窗口 Loaded 及遮罩关闭时应用；
        /// 运行中的切换由 GeneralSettingViewModel 调用 ShowWebUi/HideWebUi）
        /// </summary>
        public void ApplyUiEngineFromConfig()
        {
            if (string.Equals(_configurationService.General.UiEngine, "Web", StringComparison.OrdinalIgnoreCase))
                ShowWebUi();
            else
                HideWebUi();
        }

        public void ShowWebUi()
        {
            try
            {
                if (WebUI.CoreWebView2 != null)
                {
                    // 已初始化完成：隐藏/显示不会销毁内容，直接恢复可见即可，避免重新导航丢状态
                    WebUI.Visibility = Visibility.Visible;
                    return;
                }
#if DEBUG
                // 前端尚未创建（Task 10+），连接失败页为预期表现
                WebUI.Source = new Uri("http://localhost:5173");
#else
                if (_1RM.Service.WebUi.WebUiServer.IsRunning == false)
                {
                    SimpleLogHelper.Warning("WebUiServer is not running, skip Web UI");
                    WebUI.Visibility = Visibility.Collapsed;
                    return;
                }
                WebUI.Source = new Uri($"http://127.0.0.1:{_1RM.Service.WebUi.WebUiServer.Port}/?token={_1RM.Service.WebUi.WebUiServer.Token}");
#endif
                WebUI.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                // WebView2 运行时缺失等同步异常不阻断桌面版（异步失败见 CoreWebView2InitializationCompleted）
                SimpleLogHelper.Error(ex);
                WebUI.Visibility = Visibility.Collapsed;
            }
        }

        public void HideWebUi()
        {
            // 仅折叠即可；Source 置 null 在 WebView2 1.0.x 会抛 NotImplementedException
            WebUI.Visibility = Visibility.Collapsed;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (this.ShowInTaskbar)
            {
#if !DEBUG
                // Honor the user's 'close button behavior = Exit' choice for Alt+F4 and the
                // taskbar 'Close window' command too, mirroring BtnClose.Click's Exit branch
                // instead of always hiding to the tray (which leaves a background process
                // with live sessions that the user believes is gone).
                if (IoC.Get<ConfigurationService>().General.CloseButtonBehavior == (int)GeneralConfig.EnumCloseButtonBehavior.Exit)
                {
                    Vm.HideMe(); // clears ShowInTaskbar so App.Close's internal close is not re-intercepted here
                    App.Close();
                    return;
                }
#endif
                Vm.HideMe();
                e.Cancel = true;
            }
            else
            {
                base.OnClosing(e);
            }
        }


        private void CommandFocusFilter_OnExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            SimpleLogHelper.Debug($"CommandFocusFilter_OnExecuted");
            if (Vm.IsShownList)
            {
                if (Vm.ActiveServerViewModel.TagListViewModel == null)
                {
                    Vm.MainFilterIsFocused = true;
                }
                else
                {
                    Vm.ActiveServerViewModel.TagsPanelViewModel.FilterIsFocused = true;
                }
            }
        }



        private void MainWindow_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (this.DataContext is MainWindowViewModel vm)
            {
                if (Keyboard.FocusedElement is TextBox)
                {
                    //SimpleLogHelper.Debug($"Current FocusedElement is " + textBox.Name);
                }
                else if (e.Key == Key.Escape && vm.IsShownList == false)
                {
                    vm.ShowList(false);
                }
                else if (e.Key != Key.LeftCtrl && e.Key != Key.RightCtrl && vm.IsShownList)
                {
                    if (Vm.ActiveServerViewModel.TagListViewModel == null)
                    {
                        Vm.MainFilterIsFocused = true;
                        Vm.MainFilterIsFocused = true;
                    }
                    else
                    {
                        Vm.ActiveServerViewModel.TagsPanelViewModel.FilterIsFocused = true;
                        Vm.ActiveServerViewModel.TagsPanelViewModel.FilterIsFocused = true;
                    }
                }
            }
        }

        private void ProcessingRing_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 等待动画实现拖拽
            if (e.ClickCount >= 2)
                return;
            WinTitleBar_OnPreviewMouseDown(sender, e);
        }


        private void MainFilter_OnKeyUp(object sender, KeyEventArgs e)
        {
            // When press Esc, clear all the search keywords, but keep selected tags;
            if (e.Key != Key.Escape || sender is TextBox == false) return;
            var s = TagAndKeywordEncodeHelper.DecodeKeyword(Vm.MainFilterString);
            Vm.SetMainFilterString(s.KeyWords.Count == 0 ? null : s.TagFilterList, null);
        }


        public override void WinTitleBar_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (Vm.TopLevelViewModel != null)
                return;
            base.WinTitleBar_OnPreviewMouseDown(sender, e);
        }
    }
}