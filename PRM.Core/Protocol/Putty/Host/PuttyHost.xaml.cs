using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PRM.Core.Protocol.Putty.Host
{
    /// <summary>
    /// PuttyHost.xaml 的交互逻辑
    /// </summary>
    public partial class PuttyHost : ProtocolHostBase
    {
        [DllImport("User32.dll")]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
        [DllImport("user32.dll")]
        public static extern int ShowWindow(IntPtr hwnd, int nCmdShow);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("User32")]
        private static extern int SetForegroundWindow(IntPtr hwnd);

        private const int GWL_STYLE = (-16);
        private const int WM_CLOSE = 0x10;
        private const int WS_CAPTION  = 0x00C00000; // 	创建一个有标题框的窗口
        private const int WS_BORDER = 0x00800000;  // 	创建一个单边框的窗口
        private const int WS_THICKFRAME = 0x00040000; // 创建一个具有可调边框的窗口
        private const int WS_VSCROLL = 0x00200000; // 创建一个有垂直滚动条的窗口。


        private const int PuttyCmdWindowMargin = 0;
        private Process PuttyProcess = null;
        private IntPtr PuttyHandle = IntPtr.Zero;
        private System.Windows.Forms.Panel panel1 = null;
        private PuttyOptions PuttyOption = null;
        private Window _parentWindow = null;
        private readonly ProtocolPutttyBase _protocolPutttyBase = null;

        public PuttyHost(ProtocolPutttyBase protocolServer, Window parentWindow) : base(protocolServer, false)
        {
            _protocolPutttyBase = protocolServer;
            _parentWindow = parentWindow;
            InitializeComponent();
        }

        public override void Conn()
        {
            Debug.Assert(_protocolPutttyBase.Id > 0);
            PuttyOption = new PuttyOptions(_protocolPutttyBase.SessionName);
            
            PuttyHandle = IntPtr.Zero;           
            //FormBorderStyle = FormBorderStyle.None;
            //WindowState = FormWindowState.Maximized;
            Task tsk = new Task(InitPutty);
            tsk.Start();


            panel1 = new System.Windows.Forms.Panel();
            panel1.BackColor = System.Drawing.Color.Transparent;
            panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            //panel1.Parent = FormsHost.;
            panel1.SizeChanged += Panel1OnSizeChanged;
            FormsHost.Child = panel1;
        }

        public override void DisConn()
        {
                Close();
        }
        
        private void Panel1OnSizeChanged(object sender, EventArgs e)
        {
            if (PuttyHandle != IntPtr.Zero)
                MoveWindow(PuttyHandle, -PuttyCmdWindowMargin, -PuttyCmdWindowMargin, panel1.Width + PuttyCmdWindowMargin, panel1.Height + PuttyCmdWindowMargin, true);
        }

        public void Close()
        {
            //PostMessage(AppWindow, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            PuttyProcess?.Kill();
            PuttyProcess = null;
            DeletePuttySession();
            //MessageBox.Show("PostMessage Close");
        }

        private void InitPutty()
        {
            CreatePuttySession();
            PuttyProcess = new Process();
            var ps = new ProcessStartInfo();
            ps.FileName = @"C:\putty60.exe";
            // var arg = $"-ssh {port} {user} {pw} {server}";
            // var arg = $@" -load ""{PuttyOption.SessionName}"" {IP} -P {PORT} -l {user} -pw {pdw} -{ssh version}";
            ps.Arguments = _protocolPutttyBase.GetPuttyConnString();
            ps.WindowStyle = ProcessWindowStyle.Minimized;
            PuttyProcess.StartInfo = ps;
            PuttyProcess.Start();
            PuttyProcess.Exited += (sender, args) => PuttyProcess = null;
            PuttyProcess.Refresh();
            PuttyProcess.WaitForInputIdle();
            PuttyHandle = PuttyProcess.MainWindowHandle;

            Dispatcher.Invoke(() =>
            {
                SetParent(PuttyHandle, panel1.Handle);
                var wih = new WindowInteropHelper(_parentWindow);
                IntPtr hWnd = wih.Handle;
                SetForegroundWindow(hWnd);
                ShowWindow(PuttyHandle, 3); //SW_SHOWMAXIMIZED
                int lStyle = GetWindowLong(PuttyHandle, GWL_STYLE);
                lStyle &= ~(WS_CAPTION | WS_BORDER | WS_THICKFRAME);
                SetWindowLong(PuttyHandle, GWL_STYLE, lStyle); // make putty "WindowStyle=None"
                MoveWindow(PuttyHandle, -PuttyCmdWindowMargin, -PuttyCmdWindowMargin, panel1.Width + PuttyCmdWindowMargin, panel1.Height + PuttyCmdWindowMargin, true);
                DeletePuttySession();
            });
        }
        


        private void CreatePuttySession()
        {
            PuttyOption.Set(PuttyRegOptionKey.FontHeight, 14);
            PuttyOption.Set(PuttyRegOptionKey.Colour0, "255,255,255");
            PuttyOption.Set(PuttyRegOptionKey.Colour1, "255,255,255");
            PuttyOption.Set(PuttyRegOptionKey.Colour2, "51,51,51");
            PuttyOption.Set(PuttyRegOptionKey.Colour3, "85,85,85");
            PuttyOption.Set(PuttyRegOptionKey.Colour4, "0,0,0");
            PuttyOption.Set(PuttyRegOptionKey.Colour5, "0,255,0");
            PuttyOption.Set(PuttyRegOptionKey.Colour6, "77,77,77");
            PuttyOption.Set(PuttyRegOptionKey.Colour7, "85,85,85");
            PuttyOption.Set(PuttyRegOptionKey.Colour8, "187,0,0");
            PuttyOption.Set(PuttyRegOptionKey.Colour9, "255,85,85");
            PuttyOption.Set(PuttyRegOptionKey.Colour10, "152,251,152");
            PuttyOption.Set(PuttyRegOptionKey.Colour11, "85,255,85");
            PuttyOption.Set(PuttyRegOptionKey.Colour12, "240,230,140");
            PuttyOption.Set(PuttyRegOptionKey.Colour13, "255,255,85");
            PuttyOption.Set(PuttyRegOptionKey.Colour14, "205,133,63");
            PuttyOption.Set(PuttyRegOptionKey.Colour15, "135,206,235");
            PuttyOption.Set(PuttyRegOptionKey.Colour16, "255,222,173");
            PuttyOption.Set(PuttyRegOptionKey.Colour17, "255,85,255");
            PuttyOption.Set(PuttyRegOptionKey.Colour18, "255,160,160");
            PuttyOption.Set(PuttyRegOptionKey.Colour19, "255,215,0");
            PuttyOption.Set(PuttyRegOptionKey.Colour20, "245,222,179");
            PuttyOption.Set(PuttyRegOptionKey.Colour21, "255,255,255");


            //PuttyOption.Set(PuttyRegOptionKey.Colour0, "192,192,192");
            //PuttyOption.Set(PuttyRegOptionKey.Colour1, "255,255,255");
            //PuttyOption.Set(PuttyRegOptionKey.Colour2, "0,0,0");
            //PuttyOption.Set(PuttyRegOptionKey.Colour3, "85,85,85");
            //PuttyOption.Set(PuttyRegOptionKey.Colour4, "0,0,0");
            //PuttyOption.Set(PuttyRegOptionKey.Colour5, "0,255,0");
            //PuttyOption.Set(PuttyRegOptionKey.Colour6, "0,0,0");
            //PuttyOption.Set(PuttyRegOptionKey.Colour7, "85,85,85");
            //PuttyOption.Set(PuttyRegOptionKey.Colour8, "255,0,255");
            //PuttyOption.Set(PuttyRegOptionKey.Colour9, "255,85,85");
            //PuttyOption.Set(PuttyRegOptionKey.Colour10,"0,255,0");
            //PuttyOption.Set(PuttyRegOptionKey.Colour11,"85,255,85");
            //PuttyOption.Set(PuttyRegOptionKey.Colour12,"187,187,0");
            //PuttyOption.Set(PuttyRegOptionKey.Colour13,"255,255,85");
            //PuttyOption.Set(PuttyRegOptionKey.Colour14,"0,255,255");
            //PuttyOption.Set(PuttyRegOptionKey.Colour15,"0,0,255");
            //PuttyOption.Set(PuttyRegOptionKey.Colour16,"0,0,255");
            //PuttyOption.Set(PuttyRegOptionKey.Colour17,"255,85,255");
            //PuttyOption.Set(PuttyRegOptionKey.Colour18,"0,187,187");
            //PuttyOption.Set(PuttyRegOptionKey.Colour19,"85,255,255");
            //PuttyOption.Set(PuttyRegOptionKey.Colour20,"187,187,187");
            //PuttyOption.Set(PuttyRegOptionKey.Colour21,"255,255,255");


            //PuttyOption.Set(PuttyRegOptionKey.UseSystemColours, 0);
            //PuttyOption.Set(PuttyRegOptionKey.TryPalette, 0);
            //PuttyOption.Set(PuttyRegOptionKey.ANSIColour, 1);
            //PuttyOption.Set(PuttyRegOptionKey.Xterm256Colour, 1);
            //PuttyOption.Set(PuttyRegOptionKey.BoldAsColour, 1);

            //PuttyOption.Set(PuttyRegOptionKey.Colour0, "211,215,207");
            //PuttyOption.Set(PuttyRegOptionKey.Colour1, "238,238,236");
            //PuttyOption.Set(PuttyRegOptionKey.Colour2, "46,52,54");
            //PuttyOption.Set(PuttyRegOptionKey.Colour3, "85,87,83");
            //PuttyOption.Set(PuttyRegOptionKey.Colour4, "0,0,0");
            //PuttyOption.Set(PuttyRegOptionKey.Colour5, "0,255,0");
            //PuttyOption.Set(PuttyRegOptionKey.Colour6, "46,52,54");
            //PuttyOption.Set(PuttyRegOptionKey.Colour7, "85,87,83");
            //PuttyOption.Set(PuttyRegOptionKey.Colour8, "204,0,0");
            //PuttyOption.Set(PuttyRegOptionKey.Colour9, "239,41,41");
            //PuttyOption.Set(PuttyRegOptionKey.Colour10,"78,154,6");
            //PuttyOption.Set(PuttyRegOptionKey.Colour11,"138,226,52");
            //PuttyOption.Set(PuttyRegOptionKey.Colour12,"196,160,0");
            //PuttyOption.Set(PuttyRegOptionKey.Colour13,"252,233,79");
            //PuttyOption.Set(PuttyRegOptionKey.Colour14,"52,101,164");
            //PuttyOption.Set(PuttyRegOptionKey.Colour15,"114,159,207");
            //PuttyOption.Set(PuttyRegOptionKey.Colour16,"117,80,123");
            //PuttyOption.Set(PuttyRegOptionKey.Colour17,"173,127,168");
            //PuttyOption.Set(PuttyRegOptionKey.Colour18,"6,152,154");
            //PuttyOption.Set(PuttyRegOptionKey.Colour19,"52,226,226");
            //PuttyOption.Set(PuttyRegOptionKey.Colour20,"211,215,207");
            //PuttyOption.Set(PuttyRegOptionKey.Colour21,"238,238,236");


            PuttyOption.Save();
        }

        private void DeletePuttySession()
        {
            PuttyOption?.Del();
            PuttyOption = null;
        }

        public override void GoFullScreen()
        {
            throw new NotSupportedException("putty session can not go to full-screen mode!");
        }

        public override bool IsConnected()
        {
            throw new NotImplementedException();
        }

        public override bool IsConnecting()
        {
            throw new NotImplementedException();
        }
    }
}
