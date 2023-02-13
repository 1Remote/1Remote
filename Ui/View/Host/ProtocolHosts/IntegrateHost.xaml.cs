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

        private const int GWL_STYLE = (-16);
        private const int WM_CLOSE = 0x10;
        private const int WS_CAPTION = 0x00C00000;      // 	创建一个有标题框的窗口
        private const int WS_BORDER = 0x00800000;       // 	创建一个单边框的窗口
        private const int WS_THICKFRAME = 0x00040000;   // 创建一个具有可调边框的窗口
        private const int WS_VSCROLL = 0x00200000;      // 创建一个有垂直滚动条的窗口。

        private const int SW_HIDE = 0;
        private const int SW_SHOWNORMAL = 1;
        private const int SW_SHOWMAXIMIZED = 3;

        #endregion

        private Timer? _timer;
        private Process? _process;
        private readonly System.Windows.Forms.Panel _panel;
        private readonly HashSet<IntPtr> _exeHandles = new();
        public readonly string ExeFullName;
        public readonly string ExeArguments;
        private readonly Dictionary<string, string> _environmentVariables;

        public static IntegrateHost Create(ProtocolBase protocol, string exeFullName, string exeArguments, Dictionary<string, string>? environmentVariables = null)
        {
            IntegrateHost? view = null;
            Execute.OnUIThreadSync(() =>
            {
                view = new IntegrateHost(protocol, exeFullName, exeArguments, environmentVariables);
            });
            return view!;
        }

        private IntegrateHost(ProtocolBase protocol, string exeFullName, string exeArguments, Dictionary<string, string>? environmentVariables = null) : base(protocol, false)
        {
            ExeFullName = exeFullName;
            ExeArguments = exeArguments;
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
                    ShowWindow(exeHandle, SW_SHOWMAXIMIZED);
                    int lStyle = GetWindowLong(exeHandle, GWL_STYLE);
                    lStyle &= ~WS_CAPTION; // no title
                    lStyle &= ~WS_BORDER; // no border
                    lStyle &= ~WS_THICKFRAME;
                    lStyle &= ~WS_VSCROLL;
                    SetWindowLong(exeHandle, GWL_STYLE, lStyle);
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
            if (File.Exists(ExeFullName) == false) return;

            RunBeforeConnect?.Invoke();
            var exeFullName = ExeFullName;

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
                Thread.Sleep(1 * 1000);
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