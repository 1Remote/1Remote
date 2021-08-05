using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using Microsoft.Win32;
using PRM.Core.External.KiTTY;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.Protocol.FileTransmit.SFTP;
using PRM.Core.Protocol.Putty;
using PRM.Core.Protocol.Putty.SSH;
using Shawn.Utils;
using Path = System.IO.Path;

namespace PRM.View.ProtocolHosts
{
    public partial class KittyHost : HostBase
    {
        [DllImport("User32.dll")]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll")]
        public static extern int ShowWindow(IntPtr hwnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

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

        private const int GWL_STYLE = (-16);
        private const int WM_CLOSE = 0x10;
        private const int WS_CAPTION = 0x00C00000;      // 	创建一个有标题框的窗口
        private const int WS_BORDER = 0x00800000;       // 	创建一个单边框的窗口
        private const int WS_THICKFRAME = 0x00040000;   // 创建一个具有可调边框的窗口
        private const int WS_VSCROLL = 0x00200000;      // 创建一个有垂直滚动条的窗口。

        private const int SW_HIDE = 0;
        private const int SW_SHOWNORMAL = 1;
        private const int SW_SHOWMAXIMIZED = 3;

        private const int KittyWindowMargin = 0;
        private Process _kittyProcess = null;
        private IntPtr _kittyHandleOfWindow = IntPtr.Zero;
        private System.Windows.Forms.Panel _kittyMasterPanel = null;
        private IntPtr KittyMasterPanelHandle { get; set; } = IntPtr.Zero;

        private readonly IKittyConnectable _protocolKittyBase = null;

        public KittyHost(PrmContext context, IKittyConnectable iKittyConnectable) : base(context, iKittyConnectable.ProtocolServerBase, false)
        {
            _protocolKittyBase = iKittyConnectable;
            InitializeComponent();
        }

        ~KittyHost()
        {
            Dispose(false);
        }

        public override void Conn()
        {
            Debug.Assert(ParentWindow != null);
            Debug.Assert(_protocolKittyBase.ProtocolServerBase.Id > 0);

            // set kitty bg color
            var options = SystemConfig.Instance.Theme.SelectedPuttyTheme;
            var bgOption = options?.First(x => x.Key == EnumKittyConfigKey.Colour2.ToString());
            if (bgOption != null
                && bgOption.Value.ToString().Split(',').Length == 3)
            {
                var color = bgOption.Value.ToString().Split(',');
                if (byte.TryParse(color[0], out var r)
                    && byte.TryParse(color[1], out var g)
                    && byte.TryParse(color[2], out var b))
                {
                    GridBg.Background = new SolidColorBrush(new Color()
                    {
                        A = 255,
                        R = r,
                        G = g,
                        B = b,
                    });
                }
            }

            _kittyMasterPanel = new System.Windows.Forms.Panel
            {
                BackColor = System.Drawing.Color.Transparent,
                Dock = System.Windows.Forms.DockStyle.Fill,
                BorderStyle = BorderStyle.None
            };
            _kittyMasterPanel.SizeChanged += KittyMasterPanelOnSizeChanged;
            FormsHost.Child = _kittyMasterPanel;
            KittyMasterPanelHandle = _kittyMasterPanel.Handle;

            var tsk = new Task(InitKitty);
            tsk.Start();
        }

        public override void ReConn()
        {
            CloseKitty();
            Conn();
        }

        public override void Close()
        {
            CloseKitty();
            Dispose(true);
            GC.SuppressFinalize(this);
            Status = ProtocolHostStatus.Disconnected;
            base.Close();
        }

        private void KittyMasterPanelOnSizeChanged(object sender, EventArgs e)
        {
            if (_kittyHandleOfWindow != IntPtr.Zero)
            {
                MoveWindow(_kittyHandleOfWindow, KittyWindowMargin, KittyWindowMargin, _kittyMasterPanel.Width - KittyWindowMargin * 2,
                    _kittyMasterPanel.Height - KittyWindowMargin * 2, true);
            }
        }

        private void StartKitty()
        {
            lock (this)
            {
                Status = ProtocolHostStatus.Connecting;
                // var arg = $"-ssh {port} {user} {pw} {server}";
                // var arg = $@" -load ""{PuttyOption.SessionName}"" {IP} -P {PORT} -l {user} -pw {pdw} -{ssh version}";
                var ps = new ProcessStartInfo
                {
                    FileName = _protocolKittyBase.GetKittyExeFullName(),
                    WorkingDirectory = new FileInfo(_protocolKittyBase.GetKittyExeFullName()).Directory.FullName,
                    Arguments = _protocolKittyBase.GetPuttyConnString(Context),
                    WindowStyle = ProcessWindowStyle.Minimized
                };

                Debug.Assert(ps.Arguments.IndexOf(_protocolKittyBase.GetSessionName(), StringComparison.Ordinal) >= 0);

                _kittyProcess = new Process { StartInfo = ps };
                _kittyProcess.EnableRaisingEvents = true;
                _kittyProcess.Exited += KittyProcessOnExited;
                _kittyProcess.Start();
                SimpleLogHelper.Debug($"Start KiTTY({_kittyProcess.Handle})");
                _kittyProcess.Refresh();
                _kittyProcess.WaitForInputIdle();
                _kittyHandleOfWindow = _kittyProcess.MainWindowHandle;
                Status = ProtocolHostStatus.Connected;
            }
        }

        private void KittyProcessOnExited(object sender, EventArgs e)
        {
            SimpleLogHelper.Debug($"KittyProcessOnExited Invoked!");
            Close();
        }

        private void SetKittyWindowStyle()
        {
            SimpleLogHelper.Debug("SetParent");
            // must be set or kitty will be shown out of panel
            SetParent(_kittyHandleOfWindow, KittyMasterPanelHandle);
            SimpleLogHelper.Debug("ShowWindow");
            ShowWindow(_kittyHandleOfWindow, SW_SHOWMAXIMIZED);
            SimpleLogHelper.Debug("GetWindowLong");
            int lStyle = GetWindowLong(_kittyHandleOfWindow, GWL_STYLE);
            lStyle &= ~WS_CAPTION; // no title
            lStyle &= ~WS_BORDER;  // no border
            lStyle &= ~WS_THICKFRAME;
            lStyle &= ~WS_VSCROLL;
            SimpleLogHelper.Debug("SetWindowLong");
            SetWindowLong(_kittyHandleOfWindow, GWL_STYLE, lStyle);
            SimpleLogHelper.Debug("MoveWindow");
            MoveWindow(_kittyHandleOfWindow, KittyWindowMargin, KittyWindowMargin, _kittyMasterPanel.Width - KittyWindowMargin * 2, _kittyMasterPanel.Height - KittyWindowMargin * 2, true);
            SimpleLogHelper.Debug("Del KiTTY session config");
        }

        private void InitKitty()
        {
            string sshPrivateKeyPath = "";
            if (_protocolKittyBase is ProtocolServerSSH server)
            {
                if (!string.IsNullOrEmpty(server.PrivateKey))
                {
                    // set key
                    var ppk = Context.DataService.DecryptOrReturnOriginalString(server.PrivateKey);
                    Debug.Assert(ppk != null);
                    sshPrivateKeyPath = ppk;
                }
            }

            _protocolKittyBase.InstallKitty();
            _protocolKittyBase.SetPuttySessionConfig(sshPrivateKeyPath);
            SimpleLogHelper.Debug("ParentWindowHandle = " + ParentWindowHandle);
            SimpleLogHelper.Debug("KittyMasterPanel " + KittyMasterPanelHandle);

            StartKitty();
            Thread.Sleep(100);
            SetKittyWindowStyle();
            _protocolKittyBase.DelKittySessionConfig();
        }

        private void CloseKitty()
        {
            lock (this)
            {
                _protocolKittyBase.DelKittySessionConfig();
                if (_kittyProcess != null)
                    try
                    {
                        _kittyProcess.Exited -= KittyProcessOnExited;
                    }
                    catch (Exception)
                    {
                        // ignored
                    }

                if (_kittyProcess?.HasExited == false)
                {
                    SimpleLogHelper.Debug($"Stop KiTTY({_kittyProcess.Handle})");
                    _kittyProcess?.Kill();
                }
                _kittyProcess = null;
            }
        }

        public override void GoFullScreen()
        {
            throw new NotSupportedException("kitty session can not go to full-screen mode!");
        }

        public override void MakeItFocus()
        {
            SetForegroundWindow(_kittyHandleOfWindow);
        }

        public override ProtocolHostType GetProtocolHostType()
        {
            return ProtocolHostType.Integrate;
        }

        public override IntPtr GetHostHwnd()
        {
            return _kittyHandleOfWindow;
        }

        private void Dispose(bool disposing)
        {
            if (!disposing) return;
            CloseKitty();
            _kittyProcess?.Dispose();
            _kittyMasterPanel?.Dispose();
            FormsHost?.Dispose();
        }
    }
}