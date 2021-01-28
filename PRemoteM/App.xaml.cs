using System;
using System.IO;
using System.IO.Pipes;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using PRM.Core.Model;
using PRM.Core.Protocol.Putty.Host;
using PRM.Model;
using PRM.View;
using Shawn.Utils;

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
            SimpleLogHelper.Fatal(e);
            MessageBox.Show("please contact me if you see these: \r\n\r\n\r\n" + e.Message, SystemConfig.AppName + " unhandled error!", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
            Process.Start("https://github.com/VShawn/PRemoteM/issues");
            App.Close();
        }

        private void InitLog(string appDateFolder)
        {
            SimpleLogHelper.WriteLogEnumLogLevel = SimpleLogHelper.EnumLogLevel.Warning;
            SimpleLogHelper.PrintLogEnumLogLevel = SimpleLogHelper.EnumLogLevel.Debug;

            if (!Directory.Exists(appDateFolder))
                Directory.CreateDirectory(appDateFolder);

            // init log file placement
            var logFilePath = Path.Combine(appDateFolder, "PRemoteM.log.md");
            var fi = new FileInfo(logFilePath);
            if (!fi.Directory.Exists)
                fi.Directory.Create();
            SimpleLogHelper.LogFileName = logFilePath;
        }

        private void InitExceptionHandle()
        {
#if !DEV
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
#endif
        }

        private void UnSetShortcutAutoStart(StartupEventArgs startupEvent)
        {
            // TODO delete at 2021.04
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
        }

        private void OnlyOneAppInstanceCheck()
        {
            _singleAppMutex = new Mutex(true, PipeName, out var isFirst);
            if (!isFirst)
            {
                try
                {
                    var client = new NamedPipeClientStream(PipeName);
                    client.Connect();
                    var writer = new StreamWriter(client);
                    writer.WriteLine("ActivateMe");
                    writer.Flush();
                    client.Dispose();
                }
                catch (Exception e)
                {
                    SimpleLogHelper.Warning(e);
                }

                Environment.Exit(0);
                return;
            }

            // open NamedPipeServerStream
            Task.Factory.StartNew(() =>
            {
                NamedPipeServerStream server = null;
                while (true)
                {
                    server?.Dispose();
                    server = new NamedPipeServerStream(PipeName);
                    server.WaitForConnection();
                    var reader = new StreamReader(server);
                    var line = reader.ReadLine();
                    if (string.IsNullOrEmpty(line)) return;
                    SimpleLogHelper.Debug("NamedPipeServerStream get: " + line);
                    if (line != "ActivateMe") return;
                    Dispatcher.Invoke(() =>
                    {
                        if (App.Window?.WindowState == WindowState.Minimized)
                            App.Window.WindowState = WindowState.Normal;
                        App.Window?.ActivateMe();
                    });
                }
            });
        }

        private bool InitSystemConfig(string appDateFolder)
        {
            bool isNewUser = false;

            var iniPath = Path.Combine(appDateFolder, SystemConfig.AppName + ".ini");
            SimpleLogHelper.Debug($"ini init path = {iniPath}");

#if !FOR_MICROSOFT_STORE_ONLY
            // for portable purpose
            if (Environment.CurrentDirectory.IndexOf(@"C:\Windows", StringComparison.OrdinalIgnoreCase) < 0)
            {
                var iniOnCurrentPath = Path.Combine(Environment.CurrentDirectory, SystemConfig.AppName + ".ini");
                SimpleLogHelper.Debug($"Try local ini path = {iniOnCurrentPath}");
                if (IOPermissionHelper.HasWritePermissionOnFile(iniOnCurrentPath))
                {
                    iniPath = SystemConfig.AppName + ".ini";
                    SimpleLogHelper.Debug($"Try local ini path = {iniOnCurrentPath} Pass");
                }
            }
#endif
            SimpleLogHelper.Debug($"ini finally path = {iniPath}");

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

            // if ini is not existed, then it would be a new user
            if (!File.Exists(iniPath))
            {
                isNewUser = true;
            }

            // server data holder init.
            GlobalData.Init();

            // remote window pool init.
            RemoteWindowPool.Init();

            return isNewUser;
        }

        private void InitMainWindow(bool isNewUser)
        {
            Window = new MainWindow();
            if (!SystemConfig.Instance.General.AppStartMinimized
                || isNewUser)
            {
                ActivateWindow();
            }
        }

        private void App_OnStartup(object sender, StartupEventArgs startupEvent)
        {
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory); // in case user start app in a different working dictionary.

            var appDateFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), SystemConfig.AppName);
#if DEV
            SimpleLogHelper.WriteLogEnumLogLevel = SimpleLogHelper.EnumLogLevel.Debug;
            Shawn.Utils.ConsoleManager.Show();
#endif
            InitLog(appDateFolder);
            InitExceptionHandle();
            UnSetShortcutAutoStart(startupEvent);
            OnlyOneAppInstanceCheck();

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

            bool isNewUser = InitSystemConfig(appDateFolder);
            if (isNewUser)
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var gw = new GuidanceWindow(SystemConfig.Instance);
                gw.ShowDialog();
            }

            InitMainWindow(isNewUser);
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            MainWindow = Window;

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

            InitTaskTray();
            InitLauncher();
        }

        private static void ActivateWindow()
        {
            Window.ActivateMe();
        }

        private static void InitTaskTray()
        {
            if (TaskTrayIcon != null) return;
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

        public static void ReloadTaskTrayContextMenu()
        {
            // rebuild TaskTrayContextMenu while language changed
            if (TaskTrayIcon == null) return;

            var title = new System.Windows.Forms.MenuItem(SystemConfig.AppName);
            title.Click += (sender, args) =>
            {
                System.Diagnostics.Process.Start("https://github.com/VShawn/PRemoteM");
            };
            var @break = new System.Windows.Forms.MenuItem("-");
            var linkHowToUse = new System.Windows.Forms.MenuItem(SystemConfig.Instance.Language.GetText("about_page_how_to_use"));
            linkHowToUse.Click += (sender, args) =>
            {
                System.Diagnostics.Process.Start("https://github.com/VShawn/PRemoteM/wiki");
            };
            var linkFeedback = new System.Windows.Forms.MenuItem(SystemConfig.Instance.Language.GetText("about_page_feedback"));
            linkFeedback.Click += (sender, args) =>
            {
                System.Diagnostics.Process.Start("https://github.com/VShawn/PRemoteM/issues");
            };
            var exit = new System.Windows.Forms.MenuItem(SystemConfig.Instance.Language.GetText("word_exit"));
            exit.Click += (sender, args) => App.Close();
            var child = new System.Windows.Forms.MenuItem[] { title, @break, linkHowToUse, linkFeedback, exit };
            TaskTrayIcon.ContextMenu = new System.Windows.Forms.ContextMenu(child);
        }

        private static void InitLauncher()
        {
            SearchBoxWindow = new SearchBoxWindow();
            SearchBoxWindow.SetHotKey();
        }

        public static void Close(int exitCode = 0)
        {
            try
            {
                App.Window?.Hide();
                App.Window?.Close();
                App.Window = null;
                App.SearchBoxWindow?.Hide();
                App.SearchBoxWindow?.Close();
                App.SearchBoxWindow = null;

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