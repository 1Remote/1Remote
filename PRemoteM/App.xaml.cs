using System;
using System.IO;
using System.IO.Pipes;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Windows.ApplicationModel;
using PRM.Core;
using PRM.Core.DB;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.Protocol.Putty;
using PRM.Core.Protocol.Putty.Host;
using PRM.Core.Protocol.Putty.SSH;
using PRM.Core.Protocol.RDP;
using PRM.Model;
using PRM.View;
using PRM.ViewModel;
using Shawn.Utils;
using Shawn.Utils.PageHost;

namespace PRM
{
    /*
    Defines:
        FOR_MICROSOFT_STORE             =>  Let app try to use UWP code to fit microsoft store, if not define then app support win32 only.
        FOR_MICROSOFT_STORE_ONLY        =>  Disable all functions store not recommend.Must define FOR_MICROSOFT_STORE first!!!
    */
    public partial class App : Application
    {
        private Mutex _singleAppMutex = null;
        public static MainWindow Window { get; private set; } = null;
        public static SearchBoxWindow SearchBoxWindow { get; private set; } = null;

        public static System.Windows.Forms.NotifyIcon TaskTrayIcon { get; private set; } = null;
#if DEV
        private const string PipeName = "PRemoteM_DEBUG_singlex@foxmail.com";
#else
        private const string PipeName = "PRemoteM_singlex@foxmail.com";
#endif

        private static void OnUnhandledException(Exception e)
        {
            SimpleLogHelper.Fatal(e, e.StackTrace);
            MessageBox.Show("please contact me if you see these: \r\n\r\n\r\n" + e.Message, SystemConfig.AppName + " unhandled error!", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
            Process.Start("https://github.com/VShawn/PRemoteM/issues");
            App.Close();
        }

        private void App_OnStartup(object sender, StartupEventArgs startupEvent)
        {
            SimpleLogHelper.WriteLogEnumLogLevel = SimpleLogHelper.EnumLogLevel.Warning;
            SimpleLogHelper.PrintLogEnumLogLevel = SimpleLogHelper.EnumLogLevel.Debug;

            var appDateFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), SystemConfig.AppName);
            if (!Directory.Exists(appDateFolder))
                Directory.CreateDirectory(appDateFolder);

            // init log file placement
            {
                var logFilePath = Path.Combine(appDateFolder, "PRemoteM.log.md");
                var fi = new FileInfo(logFilePath);
                if (!fi.Directory.Exists)
                    fi.Directory.Create();
                SimpleLogHelper.LogFileName = logFilePath;
            }

            // init Exception handle
            {
                this.DispatcherUnhandledException += (o, args) =>
                {
                    OnUnhandledException(args.Exception);
                };
                TaskScheduler.UnobservedTaskException += (o, args) =>
                {
                    OnUnhandledException(args.Exception);
                };
                AppDomain.CurrentDomain.UnhandledException += (o, args) =>
                {

                    if (args.ExceptionObject is Exception e)
                    {
                        OnUnhandledException(e);
                    }
                    else
                    {
                        SimpleLogHelper.Fatal(args.ExceptionObject);
                    }
                };
            }

            try
            {
                // startup creation for win32
#if !FOR_MICROSOFT_STORE_ONLY
                {
                    var startupMode = Shawn.Utils.SetSelfStartingHelper.StartupMode.Normal;
                    if (startupEvent.Args.Length > 0)
                    {
                        System.Enum.TryParse(startupEvent.Args[0], out startupMode);
                    }
                    if (startupMode == Shawn.Utils.SetSelfStartingHelper.StartupMode.SetSelfStart)
                    {
                        SetSelfStartingHelper.SetSelfStartByShortcut(true);
                        Environment.Exit(0);
                    }
                    if (startupMode == Shawn.Utils.SetSelfStartingHelper.StartupMode.UnsetSelfStart)
                    {
                        SetSelfStartingHelper.SetSelfStartByShortcut(false);
                        Environment.Exit(0);
                    }
                }
#endif


                // make sure only one instance
                #region single-instance app
                {
                    _singleAppMutex = new Mutex(true, PipeName, out var isFirst);
                    if (!isFirst)
                    {
                        try
                        {
                            var client = new NamedPipeClientStream(PipeName);
                            client.Connect();
                            StreamReader reader = new StreamReader(client);
                            StreamWriter writer = new StreamWriter(client);
                            writer.WriteLine("ActivateMe");
                            writer.Flush();
                            client.Dispose();
                        }
                        catch (Exception e)
                        {
                            SimpleLogHelper.Warning(e);
                        }

                        Environment.Exit(0);
                    }
                    else
                    {
                        Task.Factory.StartNew(() =>
                        {
                            NamedPipeServerStream server = null;
                            while (true)
                            {
                                server?.Dispose();
                                server = new NamedPipeServerStream(PipeName);
                                SimpleLogHelper.Debug("NamedPipeServerStream.WaitForConnection");
                                server.WaitForConnection();

                                try
                                {
                                    var reader = new StreamReader(server);
                                    var line = reader.ReadLine();
                                    if (!string.IsNullOrEmpty(line))
                                    {
                                        SimpleLogHelper.Debug("NamedPipeServerStream get: " + line);
                                        if (line == "ActivateMe")
                                        {
                                            if (App.Window != null)
                                            {
                                                Dispatcher.Invoke(() =>
                                                {
                                                    if (App.Window.WindowState == WindowState.Minimized)
                                                        App.Window.WindowState = WindowState.Normal;
                                                    App.Window.ActivateMe();
                                                });
                                            }
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    SimpleLogHelper.Warning(e);
                                }
                            }
                        });
                    }
                }
                #endregion


#if DEV
                SimpleLogHelper.WriteLogEnumLogLevel = SimpleLogHelper.EnumLogLevel.Debug;
                Shawn.Utils.ConsoleManager.Show();
#endif

                #region system check & init

                bool isFirstTimeUser = false;
                #region Init
                {
                    var iniPath = Path.Combine(appDateFolder, SystemConfig.AppName + ".ini");

                    // for portable purpose
                    if (Environment.CurrentDirectory.IndexOf(@"C:\Windows", StringComparison.Ordinal) < 0)
                        if (File.Exists(SystemConfig.AppName + ".ini")
                            || IOPermissionHelper.HasWritePermissionOnDir("./"))
                        {
                            iniPath = SystemConfig.AppName + ".ini";
                        }

                    var ini = new Ini(iniPath);
                    var language = new SystemConfigLanguage(this.Resources, ini);
                    var general = new SystemConfigGeneral(ini);
                    var quickConnect = new SystemConfigQuickConnect(ini);
                    var theme = new SystemConfigTheme(this.Resources, ini);
                    var dataSecurity = new SystemConfigDataSecurity(ini);

                    // config create instance (settings & langs)
                    SystemConfig.Init();
                    SystemConfig.Instance.General = general;
                    SystemConfig.Instance.Language = language;
                    SystemConfig.Instance.QuickConnect = quickConnect;
                    SystemConfig.Instance.DataSecurity = dataSecurity;
                    SystemConfig.Instance.Theme = theme;




                    // if ini is not existed, then it would be a new user, open guide to set db path
                    if (!File.Exists(iniPath))
                    {
                        isFirstTimeUser = true;
                        ShutdownMode = ShutdownMode.OnExplicitShutdown;
                        var gw = new GuidanceWindow(SystemConfig.Instance);
                        gw.ShowDialog();
                    }


                    // server data holder init.
                    GlobalData.Init();

                    // remote window pool init.
                    RemoteWindowPool.Init();
                }
                #endregion

                // kill putty process
                foreach (var process in Process.GetProcessesByName(KittyHost.KittyExeName.ToLower().Replace(".exe", "")))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                    }
                }



                #endregion

                #region app start
                // main window init
                {
                    ShutdownMode = ShutdownMode.OnMainWindowClose;
                    MainWindow = Window;

                    Window = new MainWindow();
                    var page = new ServerListPage();
                    Window.Vm.ListViewPageForServerList = page;
                    if (!SystemConfig.Instance.General.AppStartMinimized
                        || isFirstTimeUser)
                    {
                        ActivateWindow();
                    }

                    // check if Db is ok
                    var res = SystemConfig.Instance.DataSecurity.CheckIfDbIsOk();
                    if (!res.Item1)
                    {
                        SimpleLogHelper.Info("Start with 'SystemConfigPage' by 'ErrorFlag'.");
                        MessageBox.Show(res.Item2, SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                        ActivateWindow();
                        Window.Vm.CmdGoSysOptionsPage.Execute(typeof(SystemConfigDataSecurity));
                    }
                    else
                    {
                        // load data
                        GlobalData.Instance.ServerListUpdate();
                    }
                }


                // task tray init
                InitTaskTray();


                // quick search launcher init 
                InitQuickSearch();
                #endregion
            }
            catch (Exception ex)
            {
                OnUnhandledException(ex);
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
                Debug.Assert(Application.GetResourceStream(new Uri("pack://application:,,,/LOGO.ico"))?.Stream != null);
                TaskTrayIcon = new System.Windows.Forms.NotifyIcon
                {
                    Text = SystemConfig.AppName,
                    Icon = new System.Drawing.Icon(Application.GetResourceStream(new Uri("pack://application:,,,/LOGO.ico")).Stream),
                    BalloonTipText = "",
                    Visible = true
                };
                ReloadTaskTrayContextMenu();
                GlobalEventHelper.OnLanguageChanged += ReloadTaskTrayContextMenu;
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
                var link_how_to_use = new System.Windows.Forms.MenuItem(SystemConfig.Instance.Language.GetText("about_page_how_to_use"));
                link_how_to_use.Click += (sender, args) =>
                {
                    System.Diagnostics.Process.Start("https://github.com/VShawn/PRemoteM/wiki");
                };
                var link_feedback = new System.Windows.Forms.MenuItem(SystemConfig.Instance.Language.GetText("about_page_feedback"));
                link_feedback.Click += (sender, args) =>
                {
                    System.Diagnostics.Process.Start("https://github.com/VShawn/PRemoteM/issues");
                };
                var exit = new System.Windows.Forms.MenuItem(SystemConfig.Instance.Language.GetText("word_exit"));
                exit.Click += (sender, args) => App.Close();
                var child = new System.Windows.Forms.MenuItem[] { title, @break, link_how_to_use, link_feedback, exit };
                //var child = new System.Windows.Forms.MenuItem[] { exit };
                TaskTrayIcon.ContextMenu = new System.Windows.Forms.ContextMenu(child);
            }
        }

        private static void InitQuickSearch()
        {
            SearchBoxWindow = new SearchBoxWindow();
            SearchBoxWindow.SetHotKey();
        }

        public static void Close(int exitCode = 0)
        {
            try
            {
                if (App.Window != null)
                {
                    App.Window.Hide();
                    App.Window.Close();
                    App.Window = null;
                }

                if (App.SearchBoxWindow != null)
                {
                    App.SearchBoxWindow.Hide();
                    App.SearchBoxWindow.Close();
                    App.SearchBoxWindow = null;
                }

                if (App.TaskTrayIcon != null)
                {
                    App.TaskTrayIcon.Visible = false;
                    App.TaskTrayIcon.Dispose();
                }

                RemoteWindowPool.Instance.Release();
            }
            finally
            {
                Environment.Exit(exitCode);
            }

        }
    }
}
