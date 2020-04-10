using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PRM;
using PRM.Core;
using PRM.Core.DB;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.Protocol.RDP;
using Shawn.Ulits.RDP;
using PRM.View;
using Shawn.Ulits;

namespace PersonalRemoteManager
{
    // 服务端可以被代理调用的类
    internal class OneServiceRemoteProvider : MarshalByRefObject
    {
        public void Activate()
        {
            if (App.Window != null)
            {
                App.Window.ActivateMe();
            }
        }
    }



    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private Mutex _singleAppMutex = null;
        public static MainWindow Window = null;
        public static SearchBoxWindow SearchBoxWindow = null;
        public static System.Windows.Forms.NotifyIcon TaskTrayIcon = null;
        private const string ServiceIpcPortName = "Ipc_VShawn_present_singlex@foxmail.com"; // 定义一个 IPC 端口
        private const string ServiceIpcRoute = "PRemoteM";

        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            try
            {
                _singleAppMutex = new Mutex(true, "PersonalRemoteManager", out var isFirst);
                if (!isFirst)
                {
                    var oneRemoteProvider = (OneServiceRemoteProvider)Activator.GetObject(typeof(OneServiceRemoteProvider), $"ipc://{ServiceIpcPortName}/{ServiceIpcRoute}");
                    oneRemoteProvider.Activate();
                    Environment.Exit(0);
                }
                else
                {
                    // ipc server init
                    var remoteProvider = new OneServiceRemoteProvider();
                    RemotingServices.Marshal(remoteProvider, ServiceIpcRoute);
                    ChannelServices.RegisterChannel(new IpcChannel(ServiceIpcPortName), false);
                }


                // app start
                {

                    // config init
                    SystemConfig.Init(this.Resources);


                    // main window init
                    {
                        Window = new MainWindow();
                        ShutdownMode = ShutdownMode.OnMainWindowClose;
                        MainWindow = Window;
                        Window.Closed += (o, args) => { AppOnClose(); };
                        if (!SystemConfig.GetInstance().General.AppStartMinimized)
                        {
                            ActivateWindow();
                        }
                    }


                    // task tray init
                    InitTaskTray();


                    // quick search init 
                    InitQuickSearch();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                MessageBox.Show(ex.StackTrace);
                Environment.Exit(-1);
            }
        }

        private static void ActivateWindow()
        {
            Window.ActivateMe();
        }

        private static void InitTaskTray()
        {
            if (TaskTrayIcon == null)
            {
                // 设置托盘
                TaskTrayIcon = new System.Windows.Forms.NotifyIcon
                {
                    Text = "TXT:XXXX系统",
                    Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetEntryAssembly().ManifestModule.Name),
                    BalloonTipText = "TXT:正在后台运行...",
                    Visible = true
                };
                ReloadTaskTrayContextMenu();

                TaskTrayIcon.MouseDoubleClick += (sender, e) =>
                {
                    if (e.Button == System.Windows.Forms.MouseButtons.Left)
                    {
                        ActivateWindow();
                    }
                };
            }
        }

        public static void ReloadTaskTrayContextMenu()
        {
            if (TaskTrayIcon != null)
            {
                //System.Windows.Forms.MenuItem version = new System.Windows.Forms.MenuItem("Ver:" + Version);
                System.Windows.Forms.MenuItem link = new System.Windows.Forms.MenuItem("TXT:主页");
                link.Click += (sender, args) =>
                {
                    string home_uri = "http://172.20.65.78:3300/";
                    System.Diagnostics.Process.Start(home_uri);
                };
                System.Windows.Forms.MenuItem exit = new System.Windows.Forms.MenuItem(SystemConfig.GetInstance().Language.GetText("button_exit"));
                exit.Click += (sender, args) => Window.Close();
                System.Windows.Forms.MenuItem[] child = new System.Windows.Forms.MenuItem[] { link, exit };
                TaskTrayIcon.ContextMenu = new System.Windows.Forms.ContextMenu(child);
            }
        }

        private static void InitQuickSearch()
        {
            SearchBoxWindow = new SearchBoxWindow();
            SearchBoxWindow.Show();
            var r = GlobalHotkeyHooker.GetInstance().Regist(
                SearchBoxWindow,
                GlobalHotkeyHooker.HotkeyModifiers.MOD_CONTROL,
                Key.M,
                () => { SearchBoxWindow.ShowMe(); });
            switch (r)
            {
                case GlobalHotkeyHooker.RetCode.Success:
                    break;
                case GlobalHotkeyHooker.RetCode.ERROR_HOTKEY_NOT_REGISTERED:
                    MessageBox.Show(SystemConfig.GetInstance().Language.GetText("info_hotkey_registered_fail"));
                    break;
                case GlobalHotkeyHooker.RetCode.ERROR_HOTKEY_ALREADY_REGISTERED:
                    MessageBox.Show(SystemConfig.GetInstance().Language.GetText("info_hotkey_already_registered"));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static void AppOnClose()
        {
            if (App.SearchBoxWindow != null)
            {
                App.SearchBoxWindow.Close();
                App.SearchBoxWindow = null;
            }

            if (App.TaskTrayIcon != null)
            {
                App.TaskTrayIcon.Visible = false;
                App.TaskTrayIcon.Dispose();
            }

            Environment.Exit(0);
        }
    }
}
