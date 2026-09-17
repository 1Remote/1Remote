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
                // 同步最大化状态给网页 topbar（max/restore 图标切换；页面未就绪时静默跳过）
                PushWindowStateToWeb();
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
                    // WebView2 不可用时网页无法接管标题栏，恢复 WPF 自绘标题栏保证窗口可用
                    SetWebShellChrome(false);
                }
            };

            // 网页 topbar 窗口控制桥：页面经 window.chrome.webview.postMessage 发送
            // {cmd:'window-minimize'|'window-maximize'|'window-restore'|'window-close'|'window-drag'|'window-state'}
            WebUI.WebMessageReceived += WebUI_OnWebMessageReceived;
            // 页面就绪后回推一次最大化状态（max/restore 图标初始正确）
            WebUI.NavigationCompleted += (_, _) => PushWindowStateToWeb();

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

            BtnClose.Click += (sender, args) => ExecuteCloseButtonBehavior();

            BtnMaximize.Click += (sender, args) => this.WindowState = (this.WindowState == WindowState.Normal) ? WindowState.Maximized : WindowState.Normal;
            BtnMinimize.Click += (sender, args) => { this.WindowState = WindowState.Minimized; };
        }

        /// <summary>
        /// 关闭按钮行为（WPF BtnClose 与网页 topbar 关闭按钮共用）：
        /// 先按 Engagement 规则弹“请求评分”遮罩，否则隐藏窗口并按 CloseButtonBehavior（退出/最小化到托盘）处理。
        /// </summary>
        private void ExecuteCloseButtonBehavior()
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
            Vm.HideMe();
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
                    SetWebShellChrome(true);
                    PushWindowStateToWeb();
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
                    SetWebShellChrome(false);
                    return;
                }
                WebUI.Source = new Uri($"http://127.0.0.1:{_1RM.Service.WebUi.WebUiServer.Port}/?token={_1RM.Service.WebUi.WebUiServer.Token}");
#endif
                WebUI.Visibility = Visibility.Visible;
                SetWebShellChrome(true);
            }
            catch (Exception ex)
            {
                // WebView2 运行时缺失等同步异常不阻断桌面版（异步失败见 CoreWebView2InitializationCompleted）
                SimpleLogHelper.Error(ex);
                WebUI.Visibility = Visibility.Collapsed;
                SetWebShellChrome(false);
            }
        }

        public void HideWebUi()
        {
            // 仅折叠即可；Source 置 null 在 WebView2 1.0.x 会抛 NotImplementedException
            WebUI.Visibility = Visibility.Collapsed;
            SetWebShellChrome(false);
        }

        /// <summary>
        /// 切换“谁拥有标题栏”：Web 引擎时网页 topbar 接管（WPF 标题行收 0、WebView2 铺满整窗）；
        /// Desktop 引擎或 Web 初始化失败时恢复 WPF 自绘标题栏（logo/搜索/系统按钮）+ 40px 顶部避让。
        /// 遮罩层（TopLevel mask）在外层 Grid，恒为全窗尺寸，不受此切换影响。
        /// </summary>
        private void SetWebShellChrome(bool webTakesOverTitleBar)
        {
            TitleRow.Height = webTakesOverTitleBar ? new GridLength(0) : new GridLength(40);
            WinTitleBar.Visibility = webTakesOverTitleBar ? Visibility.Collapsed : Visibility.Visible;
            WinSysButtons.Visibility = webTakesOverTitleBar ? Visibility.Collapsed : Visibility.Visible;
            WebUI.Margin = webTakesOverTitleBar ? new Thickness(0) : new Thickness(0, 40, 0, 0);
        }

        /// <summary>
        /// 网页 topbar 窗口控制消息处理（见构造函数中 WebUI.WebMessageReceived 的挂接注释）。
        /// </summary>
        private void WebUI_OnWebMessageReceived(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                // 页面固定 postMessage(JSON.stringify(...))（字符串）；容错兼容对象型 post（WebMessageAsJson）
                string? message;
                try
                {
                    message = e.TryGetWebMessageAsString();
                }
                catch (InvalidOperationException)
                {
                    message = e.WebMessageAsJson;
                }
                if (string.IsNullOrWhiteSpace(message))
                    return;
                var cmd = (string?)Newtonsoft.Json.Linq.JObject.Parse(message)["cmd"];
                switch (cmd)
                {
                    case "window-minimize":
                        this.WindowState = WindowState.Minimized;
                        break;
                    case "window-maximize":
                        this.WindowState = WindowState.Maximized;
                        break;
                    case "window-restore":
                        this.WindowState = WindowState.Normal;
                        break;
                    case "window-close":
                        ExecuteCloseButtonBehavior();
                        break;
                    case "window-drag":
                        DragWindowFromWeb();
                        break;
                    case "window-state":
                        PushWindowStateToWeb();
                        break;
                }
            }
            catch (Exception ex)
            {
                SimpleLogHelper.Warning($"unrecognized web message: {ex.Message}");
            }
        }

        /// <summary>
        /// 网页 topbar 请求拖动窗口：WebView2 是 HwndHost（airspace），WPF 覆盖条无法浮于其上
        /// 接收鼠标，故由页面在“按住左键并移动超过阈值”后发来 'window-drag'，此处接管拖动。
        /// 关键：不能用 WPF 的 <see cref="Mouse"/>.LeftButton 判定（Window.DragMove 内部同款检查）——
        /// 按下发生在 WebView2 子 HWND，其消息不进入 WPF 输入栈，MouseDevice 状态对 HwndHost 内
        /// 的按下不可靠（可能仍报 Released → 守卫静默返回 / DragMove 抛 InvalidOperationException
        /// 被吞，表现为“完全拖不动”）。改用 Win32 GetKeyState(VK_LBUTTON) 取物理键态，并以
        /// SendMessage(WM_SYSCOMMAND, SC_DRAG) 进入系统移动循环（DragMove 的裸实现，无 WPF 态检查）。
        /// 最大化状态先还原并让窗口中心跟随光标（与 WindowBase.WinTitleBar_OnPreviewMouseMove 一致）；
        /// ReleaseCapture 先解除 WebView2 子窗口的鼠标捕获，使 SC_DRAG 模态移动循环接管输入。
        /// </summary>
        private void DragWindowFromWeb()
        {
            if (IsLeftButtonPressed() != true)
                return;
            if (this.WindowState == WindowState.Maximized)
            {
                var p = ScreenInfoEx.GetMouseVirtualPosition();
                this.Top = p.Y - 15;
                this.Left = p.X - this.Width / 2;
                this.WindowState = WindowState.Normal;
                this.Top = p.Y - 15;
                this.Left = p.X - this.Width / 2;
            }
            try
            {
                ReleaseCapture();
                // SC_DRAG(0xF012) = DragMove 的底层实现（WM_SYSCOMMAND DefWindowProc 移动循环），
                // 绕过 Window.DragMove 的 Mouse.LeftButton 检查（该状态对 HwndHost 内按下不可靠）
                SendMessage(new System.Runtime.InteropServices.HandleRef(this, new System.Windows.Interop.WindowInteropHelper(this).Handle),
                    0x0112 /*WM_SYSCOMMAND*/, (IntPtr)0xF012 /*SC_DRAG*/, IntPtr.Zero);
            }
            catch (Exception e)
            {
                // 左键在消息往返期间已松开等，忽略本次拖拽
                SimpleLogHelper.Warning($"web drag failed: {e.Message}");
            }
        }

        /// <summary>Win32 VK_LBUTTON 物理键态（高位=按下）：与 WPF MouseDevice 不同，
        /// 不依赖消息进入 WPF 输入栈，对 WebView2（HwndHost）内的按下同样准确。</summary>
        private static bool IsLeftButtonPressed()
        {
            return (GetKeyState(0x01 /*VK_LBUTTON*/) & 0x8000) != 0;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(System.Runtime.InteropServices.HandleRef hWnd, int msg, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// 推送当前最大化状态给网页（window.__setWinState，页面侧切换 max/restore 图标）。
        /// 页面可能尚未加载完成：脚本内自带 __setWinState 存在性判断，异常静默。
        /// </summary>
        private void PushWindowStateToWeb()
        {
            if (WebUI.Visibility != Visibility.Visible || WebUI.CoreWebView2 == null)
                return;
            try
            {
                var state = this.WindowState == WindowState.Maximized ? "maximized" : "normal";
                _ = WebUI.CoreWebView2.ExecuteScriptAsync($"window.__setWinState && window.__setWinState('{state}');");
            }
            catch (Exception ex)
            {
                SimpleLogHelper.Warning(ex);
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        protected override void OnClosing(CancelEventArgs e)
        {
            if (this.ShowInTaskbar)
            {
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
            // Web 引擎：转发给网页搜索框（App.vue 注册的 window.__focusSearch）。焦点不在 WebView2
            //（如启动后未点进页面）时，窗口级 KeyBinding 会把 Ctrl+F 先派到这里——WPF 侧过滤控件
            // 已随标题栏折叠，直接走下方原逻辑对 web 无感；焦点在 WebView2 内时网页自己的
            // Ctrl+K/F handler 已生效，不会进入本路径。CoreWebView2 未初始化（初始化中/失败）时
            // 回退原 WPF 行为。脚本自带 __focusSearch 存在性判断，页面未就绪时静默无操作
            if (WebUI.Visibility == Visibility.Visible && WebUI.CoreWebView2 != null)
            {
                try
                {
                    _ = WebUI.CoreWebView2.ExecuteScriptAsync("window.__focusSearch && window.__focusSearch();");
                }
                catch (Exception ex)
                {
                    SimpleLogHelper.Warning(ex);
                }
                return;
            }
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