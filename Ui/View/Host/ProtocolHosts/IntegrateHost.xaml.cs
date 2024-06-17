using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Model.ProtocolRunner;
using _1RM.Model.ProtocolRunner.Default;
using _1RM.Utils;
using _1RM.Utils.KiTTY;
using Shawn.Utils;
using Stylet;
using Path = System.IO.Path;
using Timer = System.Timers.Timer;

/*
 * Note:

We should add <UseWindowsForms>true</UseWindowsForms> in the csproj.

<PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>netcoreapp3.0</TargetFramework>
    <UseWpf>true</UseWpf>
    <UseWindowsForms>true</UseWindowsForms>
</PropertyGroup>


 */

namespace _1RM.View.Host.ProtocolHosts
{
    public partial class IntegrateHost : HostBase, IDisposable
    {
        #region API

        [DllImport("User32.dll")]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll")]
        public static extern int ShowWindow(IntPtr hwnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        public delegate bool WndEnumProc(IntPtr hWnd, int lParam);
        [DllImport("user32.dll")]
        public static extern int EnumWindows(WndEnumProc lpEnumFunc, int lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern int SetForegroundWindow(IntPtr hwnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern IntPtr SetFocus(HandleRef hWnd);
        [DllImport("user32")]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32")]
        public static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        // https://stackoverflow.com/a/57819801/8629624
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern int GetWindowTextLength(IntPtr hWnd);

        public static string GetWindowTitle(IntPtr hWnd)
        {
            var length = GetWindowTextLength(hWnd) + 1;
            var title = new StringBuilder(length);
            GetWindowText(hWnd, title, length);
            return title.ToString();
        }

        internal enum GetWindowLongIndex
        {
            GWL_STYLE = -16,
            GWL_EXSTYLE = -20
        }
        internal enum ShowWindowStyles : short
        {
            SW_HIDE = 0,
            SW_SHOWNORMAL = 1,
            SW_NORMAL = 1,
            SW_SHOWMINIMIZED = 2,
            SW_SHOWMAXIMIZED = 3,
            SW_MAXIMIZE = 3,
            SW_SHOWNOACTIVATE = 4,
            SW_SHOW = 5,
            SW_MINIMIZE = 6,
            SW_SHOWMINNOACTIVE = 7,
            SW_SHOWNA = 8,
            SW_RESTORE = 9,
            SW_SHOWDEFAULT = 10,
            SW_FORCEMINIMIZE = 11,
            SW_MAX = 11
        }
        internal enum WindowStyles : uint
        {
            WS_OVERLAPPED = 0x00000000,
            WS_POPUP = 0x80000000,
            WS_CHILD = 0x40000000,
            WS_MINIMIZE = 0x20000000,
            WS_VISIBLE = 0x10000000,
            WS_DISABLED = 0x08000000,
            WS_CLIPSIBLINGS = 0x04000000,
            WS_CLIPCHILDREN = 0x02000000,
            WS_MAXIMIZE = 0x01000000,
            WS_CAPTION = 0x00C00000,      // 	创建一个有标题框的窗口
            WS_BORDER = 0x00800000,       // 	创建一个单边框的窗口
            WS_DLGFRAME = 0x00400000,
            WS_VSCROLL = 0x00200000,      // 创建一个有垂直滚动条的窗口。
            WS_HSCROLL = 0x00100000,
            WS_SYSMENU = 0x00080000,
            WS_THICKFRAME = 0x00040000,   // 创建一个具有可调边框的窗口
            WS_GROUP = 0x00020000,
            WS_TABSTOP = 0x00010000,
            WS_MINIMIZEBOX = 0x00020000,
            WS_MAXIMIZEBOX = 0x00010000,
            WS_TILED = 0x00000000,
            WS_ICONIC = 0x20000000,
            WS_SIZEBOX = 0x00040000,
            WS_POPUPWINDOW = 0x80880000,
            WS_OVERLAPPEDWINDOW = 0x00CF0000,
            WS_TILEDWINDOW = 0x00CF0000,
            WS_CHILDWINDOW = 0x40000000
        }

        [Flags]
        internal enum WindowExStyles
        {
            WS_EX_DLGMODALFRAME = 0x00000001,
            WS_EX_NOPARENTNOTIFY = 0x00000004,
            WS_EX_TOPMOST = 0x00000008,
            WS_EX_ACCEPTFILES = 0x00000010,
            WS_EX_TRANSPARENT = 0x00000020,
            WS_EX_MDICHILD = 0x00000040,
            WS_EX_TOOLWINDOW = 0x00000080,
            WS_EX_WINDOWEDGE = 0x00000100,
            WS_EX_CLIENTEDGE = 0x00000200,
            WS_EX_CONTEXTHELP = 0x00000400,
            WS_EX_RIGHT = 0x00001000,
            WS_EX_LEFT = 0x00000000,
            WS_EX_RTLREADING = 0x00002000,
            WS_EX_LTRREADING = 0x00000000,
            WS_EX_LEFTSCROLLBAR = 0x00004000,
            WS_EX_RIGHTSCROLLBAR = 0x00000000,
            WS_EX_CONTROLPARENT = 0x00010000,
            WS_EX_STATICEDGE = 0x00020000,
            WS_EX_APPWINDOW = 0x00040000,
            WS_EX_OVERLAPPEDWINDOW = 0x00000300,
            WS_EX_PALETTEWINDOW = 0x00000188,
            WS_EX_LAYERED = 0x00080000,
            WS_EX_NOACTIVATE = 0x08000000
        }

        #endregion

        private Timer? _timer;
        private Process? _process;
        private readonly System.Windows.Forms.Panel _panel;
        private readonly HashSet<IntPtr> _exeHandles = new();
        public readonly string ExeFullName;
        public string ExeArguments { get; private set; }
        private readonly Dictionary<string, string> _environmentVariables;
        private readonly Runner _runner;
        private readonly string _sessionName = "";

        public static IntegrateHost Create(ProtocolBase protocol, Runner runner, string exeFullName, string exeArguments, Dictionary<string, string>? environmentVariables = null)
        {
            IntegrateHost? view = null;
            Execute.OnUIThreadSync(() =>
            {
                view = new IntegrateHost(protocol, runner, exeFullName, exeArguments, environmentVariables);
            });
            return view!;
        }

        private IntegrateHost(ProtocolBase protocol, Runner runner, string exeFullName, string exeArguments, Dictionary<string, string>? environmentVariables = null) : base(protocol, false)
        {
            ExeFullName = exeFullName;
            ExeArguments = exeArguments;
            _runner = runner;
            _environmentVariables = environmentVariables ?? new Dictionary<string, string>();
            InitializeComponent();

            _panel = new System.Windows.Forms.Panel
            {
                BackColor = System.Drawing.Color.Transparent,
                Dock = System.Windows.Forms.DockStyle.Fill,
                BorderStyle = BorderStyle.None
            };
            _panel.SizeChanged += PanelOnSizeChanged;

            FormsHost.Child = _panel;


            if (runner is KittyRunner kittyRunner)
            {
                _sessionName = $"{Assert.APP_NAME}_{protocol.Protocol}_{protocol.Id}_{DateTimeOffset.Now.ToUnixTimeSeconds()}";
                RunAfterConnected += () => PuttyConnectableExtension.DelKittySessionConfig(_sessionName, kittyRunner.PuttyExePath);
            }
        }

        #region Resize

        private void SetToPanelSize()
        {
            if (_process != null)
            {
                CleanupClosedHandle();
                foreach (var exeHandle in _exeHandles)
                {
                    MoveWindow(exeHandle, 0, 0, (int)(_panel.Width), (int)(_panel.Height), true);
                }
            }
        }

        // not work with UHD + scaling, e.g. 4k+150%, the ActualWidth will return 1500 / 150% = 1000pix while the real width is 1500pix.
        //protected override void OnRender(DrawingContext drawingContext)
        //{
        //    if (_process != null)
        //    {
        //        CleanupClosedHandle();
        //        SimpleLogHelper.Debug($"ActualWidth = {(int)(FormsHost.ActualWidth)}, ActualHeight = {(int)(FormsHost.ActualHeight)}");
        //        SimpleLogHelper.Debug($"GridActualWidth = {(int)(Grid.ActualWidth)}, GridActualHeight = {(int)(Grid.ActualHeight)}");
        //        foreach (var exeHandle in _exeHandles)
        //        {
        //            MoveWindow(exeHandle, 0, 0, (int)(Grid.ActualWidth), (int)(Grid.ActualHeight), true);
        //        }
        //        //MoveWindow(_exeHandle, 0, 0, (int)(FormsHost.ActualWidth), (int)(FormsHost.ActualHeight), true);
        //    }
        //    base.OnRender(drawingContext);
        //}

        private void PanelOnSizeChanged(object? sender, EventArgs e)
        {
            SetToPanelSize();
        }
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            this.InvalidateVisual();
            base.OnRenderSizeChanged(sizeInfo);
        }
        #endregion

        /// <summary>
        /// remove the handles in _exeHandles which is not  window
        /// </summary>
        private void CleanupClosedHandle()
        {
            foreach (var handle in _exeHandles.ToArray())
            {
                if (IsWindow(handle) == false)
                {
                    SimpleLogHelper.Debug($"_exeHandles remove {handle}");
                    _exeHandles.Remove(handle);
                }
            }
        }

        /// <summary>
        /// remove title border frame scroll of the process
        /// </summary>
        private void SetExeWindowStyle()
        {
            CleanupClosedHandle();
            Dispatcher.Invoke(() =>
            {
                foreach (var exeHandle in _exeHandles)
                {
                    // must be set or exe will be shown out of panel
                    SetParent(exeHandle, _panel.Handle);
                    ShowWindow(exeHandle, (int)ShowWindowStyles.SW_SHOWMAXIMIZED);
                    int lStyle = GetWindowLong(exeHandle, (int)GetWindowLongIndex.GWL_STYLE);
                    lStyle &= ~(int)WindowStyles.WS_CAPTION; // no title
                    lStyle &= ~(int)WindowStyles.WS_BORDER; // no border
                    lStyle &= ~(int)WindowStyles.WS_THICKFRAME;
                    //lStyle &= ~(int)WindowStyles.WS_VSCROLL;
                    //lStyle |= (int)WindowExStyles.WS_EX_TOOLWINDOW;
                    SetWindowLong(exeHandle, (int)GetWindowLongIndex.GWL_STYLE, lStyle);
                }
                SetToPanelSize();
            });
        }

        public override void Conn()
        {
            Status = ProtocolHostStatus.Connecting;
            Debug.Assert(ParentWindow != null);

            var tsk = new Task(Start);
            tsk.Start();
        }

        public override void ReConn()
        {
            CloseIntegrate();
            Conn();
        }

        public override void Close()
        {
            Dispose();
            base.Close();
        }

        public void ShowWindow(bool isShow)
        {
            foreach (var exeHandle in _exeHandles)
            {
                ShowWindow(exeHandle, (int)(isShow ? ShowWindowStyles.SW_SHOWMAXIMIZED : ShowWindowStyles.SW_HIDE));
            }
        }


        public void Dispose()
        {
            Execute.OnUIThread(() =>
            {
                CloseIntegrate();
                _timer?.Dispose();
                _process?.Dispose();
                _panel.Dispose();
                FormsHost?.Dispose();
                GC.SuppressFinalize(this);
            });
        }

        private void CloseIntegrate()
        {
            Execute.OnUIThread(() =>
            {
                _timer?.Stop();
                _timer?.Dispose();
                _timer = null;
                if (_process != null)
                {
                    try
                    {
                        _process.Exited -= ProcessOnExited;
                        _process.Kill();
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }

                Status = ProtocolHostStatus.Disconnected;
            });
        }

        public void Start()
        {
            RunBeforeConnect?.Invoke();
            var exeFullName = ExeFullName;

            if (ProtocolServer is IKittyConnectable kittyConnectable && _runner is KittyRunner kittyRunner)
            {
                // KITTY 需要根据 _sessionName 配置 cli 命令参数，所以在 start 时重新计算 cli 参数。
                ExeArguments = kittyConnectable.GetExeArguments(_sessionName);
                if (ProtocolServer is ProtocolBaseWithAddressPortUserPwd { UsePrivateKeyForConnect: true } pw)
                    kittyConnectable.ConfigKitty(_sessionName, kittyRunner, pw.PrivateKey);
                else
                    kittyConnectable.ConfigKitty(_sessionName, kittyRunner, "");
            }

            if (Path.IsPathRooted(exeFullName)
                && File.Exists(ExeFullName) == false)
            {
                MessageBoxHelper.ErrorAlert($"Exe file '{ExeFullName}' does not existed! We can not start the connection!");
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = exeFullName,
                WorkingDirectory = new FileInfo(exeFullName).DirectoryName,
                Arguments = ExeArguments,
                WindowStyle = ProcessWindowStyle.Normal
            };

            // Set environment variables
            if (this._environmentVariables?.Count > 0)
            {
                startInfo.UseShellExecute = false;
                foreach (var kv in this._environmentVariables)
                {
                    if (startInfo.EnvironmentVariables.ContainsKey(kv.Key) == false)
                        startInfo.EnvironmentVariables.Add(kv.Key, kv.Value);
                    startInfo.EnvironmentVariables[kv.Key] = kv.Value;
                }
            }

            _process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };
            _process.Exited += ProcessOnExited;
            _process.Start();

            SimpleLogHelper.Debug($"{nameof(IntegrateHost)}: Start process {exeFullName}");

            Task.Factory.StartNew(() =>
            {
                Thread.Sleep(5 * 1000);
                RunAfterConnected?.Invoke();
            });

            // keep detect MainWindowHandle in next 10 seconds.
            var endTime = DateTime.Now.AddSeconds(10);
            _timer?.Dispose();
            _timer = new Timer { Interval = 100, AutoReset = false };
            _timer.Elapsed += (sender, args) =>
            {
                _process.Refresh();
                if (_process == null)
                {
                    return;
                }
                else if (_process.MainWindowHandle != IntPtr.Zero
                    && _exeHandles.Contains(_process.MainWindowHandle) == false)
                {
                    _exeHandles.Add(_process.MainWindowHandle);
                    SimpleLogHelper.Debug($"new _process.MainWindowHandle = {_process.MainWindowHandle}");
                    SetExeWindowStyle();
                }

                if (DateTime.Now > endTime && _exeHandles.Count > 0)
                    return;
                _timer?.Start();
            };
            _timer.Start();
        }

        private void ProcessOnExited(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                _timer?.Stop();
                _timer?.Dispose();
                _timer = null;
                _process = null;
                FormsHost.Visibility = Visibility.Collapsed;
            });
            _process = null;
            Close();
        }

        public override void FocusOnMe()
        {
            SetForegroundWindow(this.GetHostHwnd());
        }

        public override ProtocolHostType GetProtocolHostType()
        {
            return ProtocolHostType.Integrate;
        }

        public override IntPtr GetHostHwnd()
        {
            if (_exeHandles.Count > 0)
                return _exeHandles.Last();
            return _process?.MainWindowHandle ?? IntPtr.Zero;
        }

        public Action? RunBeforeConnect { get; set; }
        public Action? RunAfterConnected { get; set; }
    }
}