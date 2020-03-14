using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using AxMSTSCLib;

namespace RdpRunner
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();



            this.Load += (sender, args) => { RDP(); };
        }

        private void RDP()
        {
            ((System.ComponentModel.ISupportInitialize)(rdp)).BeginInit();
            rdp.Dock = DockStyle.Fill;
            rdp.Enabled = true;
            ((System.ComponentModel.ISupportInitialize)(rdp)).EndInit();
            this.rdp.Server = "106.14.116.248";
            this.rdp.FullScreenTitle = this.rdp.Server;
            this.rdp.UserName = "administrator";
            this.rdp.AdvancedSettings2.RDPPort = 3389;
            //this.rdp.AdvancedSettings2.SmartSizing = true;
            MSTSCLib.IMsTscNonScriptable secured = (MSTSCLib.IMsTscNonScriptable)rdp.GetOcx();
            secured.ClearTextPassword = "3Pt1GHWmj2CrcISa";

            //this.rdp.Width = Convert.ToInt32(host.ActualWidth);
            //this.rdp.Height = Convert.ToInt32(host.ActualHeight
            //this.rdp.DesktopWidth = Convert.ToInt32(System.Windows.SystemParameters.PrimaryScreenWidth);
            //this.rdp.DesktopHeight = Convert.ToInt32(System.Windows.SystemParameters.PrimaryScreenHeight);
            //this.rdp.Width = 1920;
            //this.rdp.Height = 1080;

            try
            {
                //int w = int.Parse(TbW.Text);
                //int h = int.Parse(TbH.Text);
                //int w = 800;
                //int h = 600;
                Screen screen = Screen.FromControl(this); //this is the Form class
                int w = screen.Bounds.Width;
                int h = screen.Bounds.Height;
                this.rdp.Width = w;
                this.rdp.Height = h;
                this.rdp.DesktopWidth = w;
                this.rdp.DesktopHeight = h;
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }
            this.rdp.AdvancedSettings2.HotKeyFullScreen = (int)Keys.X;


            // 启用CredSSP身份验证（有些服务器连接没有反应，需要开启这个）
            rdp.AdvancedSettings7.EnableCredSspSupport = true;
            ((MSTSCLib.IMsRdpClientAdvancedSettings5)rdp.AdvancedSettings).DisplayConnectionBar = true;
            ((MSTSCLib.IMsRdpClientAdvancedSettings5)rdp.AdvancedSettings).ConnectionBarShowPinButton = true;
            ((MSTSCLib.IMsRdpClientAdvancedSettings5)rdp.AdvancedSettings).BitmapVirtualCache32BppSize = 48;
            ((MSTSCLib.IMsRdpClientAdvancedSettings5)rdp.AdvancedSettings).ConnectionBarShowMinimizeButton = true;
            ((MSTSCLib.IMsRdpClientAdvancedSettings5)rdp.AdvancedSettings).ConnectionBarShowRestoreButton = true;

            ((MSTSCLib.IMsRdpClientAdvancedSettings5)rdp.AdvancedSettings).EnableWindowsKey = 1;
            ((MSTSCLib.IMsRdpClientAdvancedSettings5)rdp.AdvancedSettings).GrabFocusOnConnect = true;
            ((MSTSCLib.IMsRdpClientAdvancedSettings5)rdp.AdvancedSettings).RedirectDrives = true;
            ((MSTSCLib.IMsRdpClientAdvancedSettings5)rdp.AdvancedSettings).RedirectClipboard = true;
            ((MSTSCLib.IMsRdpClientAdvancedSettings5)rdp.AdvancedSettings).RedirectPrinters = true;
            ((MSTSCLib.IMsRdpClientAdvancedSettings5)rdp.AdvancedSettings).RedirectPOSDevices = true;

            // 颜色位数 8,16,24,32
            rdp.ColorDepth = 32;
            //rdp.AdvancedSettings7.ContainerHandledFullScreen = 1;
            // 自动控制屏幕显示尺寸
            rdp.AdvancedSettings9.SmartSizing = true;
            rdp.AdvancedSettings8.SmartSizing = true;
            rdp.AdvancedSettings7.SmartSizing = true;
            // 禁用公共模式
            rdp.AdvancedSettings7.PublicMode = false;

            rdp.AdvancedSettings9.EnableAutoReconnect = true;

            rdp.SecuredSettings2.KeyboardHookMode = 1;
            try
            {
                this.rdp.Connect();
            }
            catch
            {
            }
            this.Width = rdp.DesktopWidth + (Width - this.DisplayRectangle.Width);
            this.Height = rdp.DesktopHeight + (Height - this.DisplayRectangle.Height);

            this.Width /= 2;
            this.Height /= 2;


            rdp.OnDisconnected += new IMsTscAxEvents_OnDisconnectedEventHandler(axMsRdpc_OnDisconnected);

        }
        private void axMsRdpc_OnDisconnected(object sender, IMsTscAxEvents_OnDisconnectedEvent e)
        {
            const int UI_ERR_NORMAL_DISCONNECT = 0xb08;
            if (e.discReason != UI_ERR_NORMAL_DISCONNECT)
            {
                string reason = rdp.GetErrorDescription((uint)e.discReason, (uint)rdp.ExtendedDisconnectReason);
                string disconnectedText = $"远程桌面 {rdp.Server} 连接已断开！{reason}";
                //rdp.DisconnectedText = disconnectedText;
                if (MessageBox.Show(disconnectedText, "远程连接") == DialogResult.OK)
                    ;
            }
            rdp.FindForm().Close();
        }
        protected override void WndProc(ref Message m)
        {
            const int WM_SYSCOMMAND = 0x112;
            const int SC_CLOSE = 0xF060;
            const int SC_MINIMIZE = 0xF020;
            const int SC_MAXIMIZE1 = 0xF030;// 最大化按钮
            const int SC_MAXIMIZE2 = 0xF032;// 双击标题栏最大化
            const int SC_RESTORE = 0xF122;
            if (m.Msg == WM_SYSCOMMAND)
            {
                if (m.WParam.ToInt32() == SC_MAXIMIZE1 ||
                    m.WParam.ToInt32() == SC_MAXIMIZE2)
                {
                    rdp.FullScreen = !rdp.FullScreen;
                    return;
                }
            }
            base.WndProc(ref m);
        }
    }
}
