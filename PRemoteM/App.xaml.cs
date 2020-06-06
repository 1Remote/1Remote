using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using System.Threading;
using System.Windows;
using PRM.Core;
using PRM.Core.DB;
using PRM.Core.Model;
using PRM.Core.Protocol.Putty.Host;
using PRM.Core.Ulits;
using PRM.Model;
using PRM.View;
using Shawn.Ulits;

namespace PRM
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
        public static MainWindow Window  { get; private set; } = null;
        public static SearchBoxWindow SearchBoxWindow { get; private set; }  = null;
        public static System.Windows.Forms.NotifyIcon TaskTrayIcon { get; private set; } = null;
#if DEBUG
        private const string ServiceIpcPortName = "Ipc_DEBUG_VShawn_present_singlex@foxmail.com";
        private const string ServiceIpcRoute = "PRemoteM_DEBUG";
#else
        private const string ServiceIpcPortName = "Ipc_VShawn_present_singlex@foxmail.com";
        private const string ServiceIpcRoute = "PRemoteM";
#endif

        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            try
            {
                #region single-instance app
                var startupMode = PRM.Core.Ulits.StartupMode.Normal;
                if (e.Args.Length > 0)
                {
                    System.Enum.TryParse(e.Args[0], out startupMode);
                }
                if (startupMode == PRM.Core.Ulits.StartupMode.SetSelfStart)
                {
                    SetSelfStartingHelper.SetSelfStart();
                    Environment.Exit(0);
                }
                if (startupMode == PRM.Core.Ulits.StartupMode.UnsetSelfStart)
                {
                    SetSelfStartingHelper.UnsetSelfStart();
                    Environment.Exit(0);
                }
#if DEBUG
                _singleAppMutex = new Mutex(true, "PRemoteM_DEBUG", out var isFirst);
#else
                _singleAppMutex = new Mutex(true, "PRemoteM", out var isFirst);
#endif
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
                #endregion

                
#if DEBUG
                Shawn.Ulits.ConsoleManager.Show();
#endif

                #region system check & init
                
                // config create instance (settings & langs)
                SystemConfig.Create(this.Resources);
                // global init
                Global.GetInstance().OnServerConn += WindowPool.ShowRemoteHost;

                // kill putty process
                foreach (var process in Process.GetProcessesByName(PuttyHost.PuttyExeName.ToLower().Replace(".exe", "")))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                    }
                }

                bool goToSettingPageFlag = false;

                // run check
                ReCheck:
                // check if Db is ok
                if (!AppChecker.CheckDbExisted())
                {
                    goToSettingPageFlag = true;
                }
                if (!AppChecker.CheckDbEncrypted().Item1)
                {
                    goToSettingPageFlag = true;
                }

                #endregion


                #region app start
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

                    if (goToSettingPageFlag)
                    {
                        SimpleLogHelper.Info("Start with 'SystemConfigPage' by 'ErroFlag'.");
                        ActivateWindow();
                        Window.VmMain.CmdGoSysOptionsPage.Execute(typeof(SystemConfigDataSecurity));
                    }
                }


                // task tray init
                InitTaskTray();


                // quick search init 
                InitQuickSearch();
                #endregion


                if (!goToSettingPageFlag)
                {
                    // load data
                    Global.GetInstance().ReloadServers();
                }
            }
            catch (Exception ex)
            {
                SimpleLogHelper.Fatal(ex.Message, ex.StackTrace);
#if DEBUG
                MessageBox.Show(ex.Message);
                MessageBox.Show(ex.StackTrace);
#endif
                AppOnClose(-1);
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
                TaskTrayIcon = new System.Windows.Forms.NotifyIcon
                {
                    Text = SystemConfig.AppName,
                    Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetEntryAssembly().ManifestModule.Name),
                    //BalloonTipText = "TXT:正在后台运行...",
                    BalloonTipText = "",
                    Visible = true
                };
                ReloadTaskTrayContextMenu();
                SystemConfig.GetInstance().Language.OnLanguageChanged += ReloadTaskTrayContextMenu;
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
            // rebuild TaskTrayContextMenu while language changed
            if (TaskTrayIcon != null)
            {
                var title = new System.Windows.Forms.MenuItem(SystemConfig.AppName);
                title.Click += (sender, args) =>
                {
                    System.Diagnostics.Process.Start("https://github.com/VShawn/PRemoteM");
                };
                var @break = new System.Windows.Forms.MenuItem("-");
                var link_how_to_use = new System.Windows.Forms.MenuItem(SystemConfig.GetInstance().Language.GetText("about_page_how_to_use"));
                link_how_to_use.Click += (sender, args) =>
                {
                    System.Diagnostics.Process.Start("https://github.com/VShawn/PRemoteM/wiki");
                };
                var link_feedback = new System.Windows.Forms.MenuItem(SystemConfig.GetInstance().Language.GetText("about_page_feedback"));
                link_feedback.Click += (sender, args) =>
                {
                    System.Diagnostics.Process.Start("https://github.com/VShawn/PRemoteM/issues");
                };
                var exit = new System.Windows.Forms.MenuItem(SystemConfig.GetInstance().Language.GetText("button_exit"));
                exit.Click += (sender, args) => Window.Close();
                var child = new System.Windows.Forms.MenuItem[] { title,@break,link_how_to_use,link_feedback, exit };
                //var child = new System.Windows.Forms.MenuItem[] { exit };
                TaskTrayIcon.ContextMenu = new System.Windows.Forms.ContextMenu(child);
            }
        }

        private static void InitQuickSearch()
        {
            SearchBoxWindow = new SearchBoxWindow();
            SearchBoxWindow.SetHotKey();
        }

        private static void AppOnClose(int exitCode = 0)
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

            Environment.Exit(exitCode);
        }
    }
}
