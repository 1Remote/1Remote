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
using System.Windows.Media;
using Microsoft.Win32;
using PRM.Core.I;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.Protocol.FileTransmit.SFTP;
using PRM.Core.Protocol.Putty;
using PRM.Core.Protocol.Putty.SSH;
using Shawn.Utils;
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

namespace PRM.View.ProtocolHosts
{
    public partial class IntegrateHost : HostBase
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

        private Process _process = null;
        private readonly System.Windows.Forms.Panel _panel;
        private readonly HashSet<IntPtr> _exeHandles = new HashSet<IntPtr>();
        private Timer _timer = null;
        public readonly string ExeFullName;
        public readonly string ExeArguments;

        public IntegrateHost(PrmContext context, ProtocolServerBase protocolServer, string exeFullName, string exeArguments) : base(context, protocolServer, false)
        {
            ExeFullName = exeFullName;
            ExeArguments = exeArguments;
            InitializeComponent();

            _panel = new System.Windows.Forms.Panel
            {
                BackColor = System.Drawing.Color.Transparent,
                Dock = System.Windows.Forms.DockStyle.Fill,
                BorderStyle = BorderStyle.None
            };

            FormsHost.Child = _panel;
        }

        #region Resize
        protected override void OnRender(DrawingContext drawingContext)
        {
            if (_process != null)
            {
                CleanupClosedHandle();

                foreach (var exeHandle in _exeHandles)
                {
                    MoveWindow(exeHandle, 0, 0, (int)(FormsHost.ActualWidth), (int)(FormsHost.ActualHeight), true);
                }
                //MoveWindow(_exeHandle, 0, 0, (int)(FormsHost.ActualWidth), (int)(FormsHost.ActualHeight), true);
            }
            base.OnRender(drawingContext);
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
                    Console.WriteLine($"_exeHandles remove {handle}");
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
                foreach (var handle in _exeHandles)
                {
                    // must be set or exe will be shown out of panel
                    SetParent(handle, _panel.Handle);
                    ShowWindow(handle, SW_SHOWMAXIMIZED);
                    int lStyle = GetWindowLong(handle, GWL_STYLE);
                    lStyle &= ~WS_CAPTION; // no title
                    lStyle &= ~WS_BORDER; // no border
                    lStyle &= ~WS_THICKFRAME;
                    lStyle &= ~WS_VSCROLL;
                    SetWindowLong(handle, GWL_STYLE, lStyle);
                    MoveWindow(handle, 0, 0, (int)(FormsHost.ActualWidth), (int)(FormsHost.ActualHeight), true);
                }
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
            CloseIntegrate();
            GC.SuppressFinalize(this);
            Status = ProtocolHostStatus.Disconnected;
            base.Close();
        }

        private void CloseIntegrate()
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
        }

        public void Start()
        {
            RunBeforeConnect?.Invoke();
            var exeFullName = ExeFullName;
            Debug.Assert(File.Exists(exeFullName));
            var ps = new ProcessStartInfo
            {
                FileName = exeFullName,
                WorkingDirectory = new FileInfo(exeFullName).DirectoryName,
                Arguments = ExeArguments,
                WindowStyle = ProcessWindowStyle.Minimized
            };

            _process = new Process
            {
                StartInfo = ps,
                EnableRaisingEvents = true
            };
            _process.Exited += ProcessOnExited;
            _process.Start();
            Console.WriteLine($"Start process {exeFullName}");

            Task.Factory.StartNew(() =>
            {
                const int waitSecond = 4;
                const int sleepMs = 10;
                for (int i = 0; i < waitSecond * 1000 / sleepMs; i++)
                {
                    Thread.Sleep(sleepMs);
                    _process.Refresh();
                    if (_process.MainWindowHandle == IntPtr.Zero)
                    {
                        continue;
                    }
                    else
                    {
                        _exeHandles.Add(_process.MainWindowHandle);
                        break;
                    }
                }

                if (_exeHandles.Count == 0)
                {
                    Console.WriteLine($"_process.Start(); can not get MainWindowHandle in {waitSecond}s");
                    Close();
                }
                else
                {
                    SetExeWindowStyle();
                    _timer?.Dispose();
                    _timer = new Timer { Interval = 100, AutoReset = false };
                    _timer.Elapsed += (sender, args) =>
                    {
                        if (_process == null)
                            return;
                        _timer.Start();
                        if (_process.MainWindowHandle == IntPtr.Zero || _exeHandles.Contains(_process.MainWindowHandle))
                        {
                            _process.Refresh();
                        }
                        else
                        {
                            var title = GetWindowTitle(_process.MainWindowHandle);
                            Console.WriteLine($"new handle = {_process.MainWindowHandle}, title = {title}");
                            if (string.IsNullOrWhiteSpace(title))
                                return;
                            _exeHandles.Add(_process.MainWindowHandle);
                            SetExeWindowStyle();
                        }
                    };
                    _timer.Start();
                }
                RunAfterConnected?.Invoke();
                Status = ProtocolHostStatus.Connected;
            });
        }

        private void ProcessOnExited(object? sender, EventArgs e)
        {
            Console.WriteLine($"ProcessOnExited");
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


        public override void GoFullScreen()
        {
            throw new NotSupportedException("Integrate session can not go to full-screen mode!");
        }

        public override void MakeItFocus()
        {
            SetForegroundWindow(_process.MainWindowHandle);
        }

        public override ProtocolHostType GetProtocolHostType()
        {
            return ProtocolHostType.Integrate;
        }

        public override IntPtr GetHostHwnd()
        {
            return _process?.MainWindowHandle ?? IntPtr.Zero;
        }

        public Action RunBeforeConnect { get; set; }
        public Action RunAfterConnected { get; set; }
    }
}