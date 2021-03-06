using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using PRM.Core.Model;
using PRM.Core.Protocol.Putty.SSH;
using Shawn.Utils;
using MessageBox = System.Windows.MessageBox;
using Path = System.IO.Path;

namespace PRM.Core.Protocol.Putty.Host
{
    public partial class KittyHost : ProtocolHostBase, IDisposable
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
#if DEV
        public const string KittyExeName = "kitty_portable_PRemoteM_debug.exe";
#else
        public const string KittyExeName = "kitty_portable_PRemoteM.exe";
#endif
        public const string KittyIniName = "kitty.ini";
        private readonly string KittyExeFolderPath = null;
        private readonly string KittyExeFullName = null;

        private const int KittyWindowMargin = 0;
        private Process _kittyProcess = null;
        private IntPtr _kittyHandleOfWindow = IntPtr.Zero;
        private System.Windows.Forms.Panel _kittyMasterPanel = null;
        private IntPtr KittyMasterPanelHandle { get; set; } = IntPtr.Zero;

        private readonly IPuttyConnectable _protocolPuttyBase = null;

        public KittyHost(PrmContext context, IPuttyConnectable iPuttyConnectable) : base(context, iPuttyConnectable.ProtocolServerBase, false)
        {
            _protocolPuttyBase = iPuttyConnectable;
            InitializeComponent();


            KittyExeFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), SystemConfig.AppName, "Kitty");
            if (!Directory.Exists(KittyExeFolderPath))
                Directory.CreateDirectory(KittyExeFolderPath);
            KittyExeFullName = Path.Combine(KittyExeFolderPath, KittyExeName);
        }

        ~KittyHost()
        {
            Dispose(false);
        }

        public override void Conn()
        {
            Debug.Assert(ParentWindow != null);
            Debug.Assert(_protocolPuttyBase.ProtocolServerBase.Id > 0);

            // set putty bg color
            var options = SystemConfig.Instance.Theme.SelectedPuttyTheme;
            var bgOption = options?.First(x => x.Key == EnumKittyOptionKey.Colour2.ToString());
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

        private void InstallKitty()
        {
            if (!Directory.Exists(KittyExeFolderPath))
                Directory.CreateDirectory(KittyExeFolderPath);

            if (File.Exists(KittyExeFullName))
            {
#if !DEV
                // verify MD5
                var md5 = MD5Helper.GetMd5Hash32BitString(File.ReadAllBytes(KittyExeFullName));
                var kitty = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/PRM.Core;component/kitty_portable.exe")).Stream;
                byte[] bytes = new byte[kitty.Length];
                kitty.Read(bytes, 0, bytes.Length);
                var md5_2 = MD5Helper.GetMd5Hash32BitString(bytes);
                if (md5_2 != md5)
                {
                    foreach (var process in Process.GetProcessesByName(KittyExeName.ToLower().ReplaceLast(".exe", "")))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch
                        {
                        }
                    }
                    File.Delete(KittyExeFullName);
                    using var fileStream = File.Create(KittyExeFullName);
                    kitty.Seek(0, SeekOrigin.Begin);
                    kitty.CopyTo(fileStream);
                }
#endif
            }
            else
            {
                var kitty = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/PRM.Core;component/kitty_portable.exe")).Stream;
                using (var fileStream = File.Create(KittyExeFullName))
                {
                    kitty.Seek(0, SeekOrigin.Begin);
                    kitty.CopyTo(fileStream);
                }
                kitty.Close();
            }


            File.WriteAllText(Path.Combine(KittyExeFolderPath, KittyIniName),
                @"
[Agent]
[ConfigBox]
dblclick=open
filter=yes
height=21
[KiTTY]
adb=yes
; antiidle: character string regularly sent to maintain the connection alive
antiidle=\k08\
; antiidledelay: time delay between two sending
antiidledelay=60
; autoreconnect: enable/disable the automatic reconnection feature
autoreconnect=yes
backgroundimage=no
capslock=no
conf=yes
ctrltab=no
cygterm=no
hyperlink=yes
icon=no
maxblinkingtime=5
mouseshortcuts=yes
paste=no
ReconnectDelay=5
size=no
transparency=yes
userpasssshnosave=no
winrol=yes
wintitle=yes
zmodem=yes
[Shortcuts]
;input=SHIFT+CONTROL+ALT+F11
;inputm=SHIFT+CONTROL+ALT+F12
;rollup=SHIFT+CONTROL+ALT+F10
[Print]
height=100
maxline=60
maxchar=85
[Launcher]
reload=yes
");
        }

        private void StartKitty()
        {
            lock (this)
            {
                // var arg = $"-ssh {port} {user} {pw} {server}";
                // var arg = $@" -load ""{PuttyOption.SessionName}"" {IP} -P {PORT} -l {user} -pw {pdw} -{ssh version}";
                //ps.Arguments = _protocolPuttyBase.GetPuttyConnString();
                var ps = new ProcessStartInfo
                {
                    FileName = KittyExeFullName,
                    WorkingDirectory = KittyExeFolderPath,
                    Arguments = _protocolPuttyBase.GetPuttyConnString(Context),
                    WindowStyle = ProcessWindowStyle.Minimized
                };

                Debug.Assert(ps.Arguments.IndexOf(_protocolPuttyBase.GetSessionName(), StringComparison.Ordinal) >= 0);

                _kittyProcess = new Process { StartInfo = ps };
                _kittyProcess.EnableRaisingEvents = true;
                _kittyProcess.Exited += KittyProcessOnExited;
                _kittyProcess.Start();
                SimpleLogHelper.Debug($"Start KiTTY({_kittyProcess.Handle})");
                _kittyProcess.Refresh();
                _kittyProcess.WaitForInputIdle();
                _kittyHandleOfWindow = _kittyProcess.MainWindowHandle; 
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
            // must be set or putty will be shown out of panel
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
            SetPuttySessionConfig();
            InstallKitty();
            SimpleLogHelper.Debug("ParentWindowHandle = " + ParentWindowHandle);
            SimpleLogHelper.Debug("KittyMasterPanel " + KittyMasterPanelHandle);
            StartKitty();
            Thread.Sleep(100);
            SetKittyWindowStyle();
            DelKittySessionConfig();
        }

        private void CloseKitty()
        {
            lock (this)
            {
                DelKittySessionConfig();
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


        private void SetPuttySessionConfig()
        {
            var puttyOption = new PuttyOptions(_protocolPuttyBase.GetSessionName(), _protocolPuttyBase.ExternalKittySessionConfigPath);
            if (_protocolPuttyBase is ProtocolServerSSH server)
            {
                if (!string.IsNullOrEmpty(server.PrivateKey))
                {
                    // set key
                    var ppk = Context.DbOperator.DecryptOrReturnOriginalString(server.PrivateKey);
                    Debug.Assert(ppk != null);
                    puttyOption.Set(EnumKittyOptionKey.PublicKeyFile, ppk);
                }
#if UseKiTTY
                puttyOption.Set(EnumKittyOptionKey.HostName, server.Address);
                puttyOption.Set(EnumKittyOptionKey.PortNumber, server.GetPort());
                puttyOption.Set(EnumKittyOptionKey.Protocol, "ssh");
#endif
            }

            // set color theme
            puttyOption.Set(EnumKittyOptionKey.FontHeight, SystemConfig.Instance.Theme.PuttyFontSize);
            var options = SystemConfig.Instance.Theme.SelectedPuttyTheme;
            if (options != null)
                foreach (var option in options)
                {
                    try
                    {
                        if (Enum.TryParse(option.Key, out EnumKittyOptionKey key))
                        {
                            if (option.ValueKind == RegistryValueKind.DWord)
                                puttyOption.Set(key, (int)(option.Value));
                            else
                                puttyOption.Set(key, (string)option.Value);
                        }
                    }
                    catch (Exception)
                    {
                        SimpleLogHelper.Warning($"Putty theme error: can't set up key(value)=> {option.Key}({option.ValueKind})");
                    }
                }

            puttyOption.Set(EnumKittyOptionKey.FontHeight, SystemConfig.Instance.Theme.PuttyFontSize);

            //_puttyOption.Set(PuttyRegOptionKey.Colour0, "255,255,255");
            //_puttyOption.Set(PuttyRegOptionKey.Colour1, "255,255,255");
            //_puttyOption.Set(PuttyRegOptionKey.Colour2, "51,51,51");
            //_puttyOption.Set(PuttyRegOptionKey.Colour3, "85,85,85");
            //_puttyOption.Set(PuttyRegOptionKey.Colour4, "0,0,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour5, "0,255,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour6, "77,77,77");
            //_puttyOption.Set(PuttyRegOptionKey.Colour7, "85,85,85");
            //_puttyOption.Set(PuttyRegOptionKey.Colour8, "187,0,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour9, "255,85,85");
            //_puttyOption.Set(PuttyRegOptionKey.Colour10, "152,251,152");
            //_puttyOption.Set(PuttyRegOptionKey.Colour11, "85,255,85");
            //_puttyOption.Set(PuttyRegOptionKey.Colour12, "240,230,140");
            //_puttyOption.Set(PuttyRegOptionKey.Colour13, "255,255,85");
            //_puttyOption.Set(PuttyRegOptionKey.Colour14, "205,133,63");
            //_puttyOption.Set(PuttyRegOptionKey.Colour15, "135,206,235");
            //_puttyOption.Set(PuttyRegOptionKey.Colour16, "255,222,173");
            //_puttyOption.Set(PuttyRegOptionKey.Colour17, "255,85,255");
            //_puttyOption.Set(PuttyRegOptionKey.Colour18, "255,160,160");
            //_puttyOption.Set(PuttyRegOptionKey.Colour19, "255,215,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour20, "245,222,179");
            //_puttyOption.Set(PuttyRegOptionKey.Colour21, "255,255,255");


            //_puttyOption.Set(PuttyRegOptionKey.Colour0, "192,192,192");
            //_puttyOption.Set(PuttyRegOptionKey.Colour1, "255,255,255");
            //_puttyOption.Set(PuttyRegOptionKey.Colour2, "0,0,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour3, "85,85,85");
            //_puttyOption.Set(PuttyRegOptionKey.Colour4, "0,0,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour5, "0,255,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour6, "0,0,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour7, "85,85,85");
            //_puttyOption.Set(PuttyRegOptionKey.Colour8, "255,0,255");
            //_puttyOption.Set(PuttyRegOptionKey.Colour9, "255,85,85");
            //_puttyOption.Set(PuttyRegOptionKey.Colour10,"0,255,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour11,"85,255,85");
            //_puttyOption.Set(PuttyRegOptionKey.Colour12,"187,187,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour13,"255,255,85");
            //_puttyOption.Set(PuttyRegOptionKey.Colour14,"0,255,255");
            //_puttyOption.Set(PuttyRegOptionKey.Colour15,"0,0,255");
            //_puttyOption.Set(PuttyRegOptionKey.Colour16,"0,0,255");
            //_puttyOption.Set(PuttyRegOptionKey.Colour17,"255,85,255");
            //_puttyOption.Set(PuttyRegOptionKey.Colour18,"0,187,187");
            //_puttyOption.Set(PuttyRegOptionKey.Colour19,"85,255,255");
            //_puttyOption.Set(PuttyRegOptionKey.Colour20,"187,187,187");
            //_puttyOption.Set(PuttyRegOptionKey.Colour21,"255,255,255");


            //_puttyOption.Set(PuttyRegOptionKey.UseSystemColours, 0);
            //_puttyOption.Set(PuttyRegOptionKey.TryPalette, 0);
            //_puttyOption.Set(PuttyRegOptionKey.ANSIColour, 1);
            //_puttyOption.Set(PuttyRegOptionKey.Xterm256Colour, 1);
            //_puttyOption.Set(PuttyRegOptionKey.BoldAsColour, 1);

            //_puttyOption.Set(PuttyRegOptionKey.Colour0, "211,215,207");
            //_puttyOption.Set(PuttyRegOptionKey.Colour1, "238,238,236");
            //_puttyOption.Set(PuttyRegOptionKey.Colour2, "46,52,54");
            //_puttyOption.Set(PuttyRegOptionKey.Colour3, "85,87,83");
            //_puttyOption.Set(PuttyRegOptionKey.Colour4, "0,0,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour5, "0,255,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour6, "46,52,54");
            //_puttyOption.Set(PuttyRegOptionKey.Colour7, "85,87,83");
            //_puttyOption.Set(PuttyRegOptionKey.Colour8, "204,0,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour9, "239,41,41");
            //_puttyOption.Set(PuttyRegOptionKey.Colour10,"78,154,6");
            //_puttyOption.Set(PuttyRegOptionKey.Colour11,"138,226,52");
            //_puttyOption.Set(PuttyRegOptionKey.Colour12,"196,160,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour13,"252,233,79");
            //_puttyOption.Set(PuttyRegOptionKey.Colour14,"52,101,164");
            //_puttyOption.Set(PuttyRegOptionKey.Colour15,"114,159,207");
            //_puttyOption.Set(PuttyRegOptionKey.Colour16,"117,80,123");
            //_puttyOption.Set(PuttyRegOptionKey.Colour17,"173,127,168");
            //_puttyOption.Set(PuttyRegOptionKey.Colour18,"6,152,154");
            //_puttyOption.Set(PuttyRegOptionKey.Colour19,"52,226,226");
            //_puttyOption.Set(PuttyRegOptionKey.Colour20,"211,215,207");
            //_puttyOption.Set(PuttyRegOptionKey.Colour21,"238,238,236");


            puttyOption.SaveToKittyConfig(KittyExeFolderPath);
        }

        private void DelKittySessionConfig()
        {
            var puttyOption = new PuttyOptions(_protocolPuttyBase.GetSessionName());
            puttyOption.DelFromKittyConfig(KittyExeFolderPath);
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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
