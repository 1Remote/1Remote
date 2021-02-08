using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using PRM.Core.DB;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.Protocol.Putty.Host;
using PRM.Model;
using PRM.View;
using PRM.View.ErrorReport;
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
        private OnlyOneAppInstanceHelper _onlyOneAppInstanceHelper;

        public static MainWindow Window { get; private set; } = null;
        public static SearchBoxWindow SearchBoxWindow { get; private set; } = null;
        public static readonly PrmContext Context = new PrmContext();

        public static System.Windows.Forms.NotifyIcon TaskTrayIcon { get; private set; } = null;

        private DesktopResolutionWatcher _desktopResolutionWatcher;

        private static void OnUnhandledException(Exception e)
        {
            SimpleLogHelper.Fatal(e);
            var errorReport = new ErrorReportWindow(e);
            errorReport.ShowDialog();
            App.Close();
        }

        private void InitLog(string appDateFolder)
        {
            SimpleLogHelper.WriteLogEnumLogLevel = SimpleLogHelper.EnumLogLevel.Warning;
            SimpleLogHelper.PrintLogEnumLogLevel = SimpleLogHelper.EnumLogLevel.Debug;
            // init log file placement
            var logFilePath = Path.Combine(appDateFolder, $"{SystemConfig.AppName}.log.md");
            var fi = new FileInfo(logFilePath);
            if (!fi.Directory.Exists)
                fi.Directory.Create();
            SimpleLogHelper.LogFileName = logFilePath;
        }

        private void InitExceptionHandle()
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

        private void UnSetShortcutAutoStart(StartupEventArgs startupEvent)
        {
            // TODO delete at 2021.04
#if !FOR_MICROSOFT_STORE_ONLY
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
#endif
        }

        private void OnlyOneAppInstanceCheck()
        {
            _onlyOneAppInstanceHelper = new OnlyOneAppInstanceHelper(SystemConfig.AppName);
            if (!_onlyOneAppInstanceHelper.IsFirstInstance())
            {
                _onlyOneAppInstanceHelper.NamedPipeSendMessage("ActivateMe");
                _onlyOneAppInstanceHelper.Dispose();
                Environment.Exit(0);
            }

            _onlyOneAppInstanceHelper.OnMessageReceived += message =>
            {
                SimpleLogHelper.Debug("NamedPipeServerStream get: " + message);
                if (message == "ActivateMe")
                    App.Window?.ActivateMe();
            };
        }

        private void InitEvent()
        {
            // Event register
            GlobalEventHelper.OnRequestDeleteServer += delegate (int id)
            {
                Context.AppData.ServerListRemove(id);
            };
            GlobalEventHelper.OnRequestUpdateServer += delegate (ProtocolServerBase server)
            {
                Context.AppData.ServerListUpdate(server);
            };
            _desktopResolutionWatcher = new DesktopResolutionWatcher();
            _desktopResolutionWatcher.OnDesktopResolutionChanged += () =>
            {
                GlobalEventHelper.OnScreenResolutionChanged?.Invoke();
            };
        }

        private bool InitSystemConfig(string appDateFolder)
        {
            var iniPath = Path.Combine(appDateFolder, SystemConfig.AppName + ".ini");
            //SimpleLogHelper.Debug($"ini init path = {iniPath}");

#if !FOR_MICROSOFT_STORE_ONLY
            // for portable purpose
            if (Environment.CurrentDirectory.IndexOf(@"C:\Windows", StringComparison.OrdinalIgnoreCase) < 0)
            {
                var iniOnCurrentPath = Path.Combine(Environment.CurrentDirectory, SystemConfig.AppName + ".ini");
                //SimpleLogHelper.Debug($"Try local ini path = {iniOnCurrentPath}");
                if (IOPermissionHelper.HasWritePermissionOnFile(iniOnCurrentPath))
                {
                    iniPath = SystemConfig.AppName + ".ini";
                    //SimpleLogHelper.Debug($"Try local ini path = {iniOnCurrentPath} Pass");
                }
            }
#endif
            //SimpleLogHelper.Debug($"ini finally path = {iniPath}");


            // if ini is not existed, then it would be a new user
            bool isNewUser = !File.Exists(iniPath);


            var ini = new Ini(iniPath);
            var language = new SystemConfigLanguage(this.Resources, ini);
            var general = new SystemConfigGeneral(ini);
            var quickConnect = new SystemConfigLauncher(ini);
            var theme = new SystemConfigTheme(this.Resources, ini);
            var locality = new SystemConfigLocality(new Ini(Path.Combine(appDateFolder, "locality.ini")));

            // read dbPath.
            var dataSecurity = new SystemConfigDataSecurity(Context, ini);

            // config create instance (settings & langs)
            SystemConfig.Init();
            SystemConfig.Instance.General = general;
            SystemConfig.Instance.Language = language;
            SystemConfig.Instance.Launcher = quickConnect;
            SystemConfig.Instance.DataSecurity = dataSecurity;
            SystemConfig.Instance.Theme = theme;
            SystemConfig.Instance.Locality = locality;

            if (isNewUser)
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var gw = new GuidanceWindow(SystemConfig.Instance);
                gw.ShowDialog();
            }

            return isNewUser;
        }

        private void KillPutty()
        {
            // kill putty process
            foreach (var process in Process.GetProcessesByName(KittyHost.KittyExeName.ToLower().Replace(".exe", "")))
            {
                process.Kill();
            }
        }

        private void InitMainWindow(bool isNewUser)
        {
            Window = new MainWindow(Context);
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            MainWindow = Window;
        }

        private void App_OnStartup(object sender, StartupEventArgs startupEvent)
        {
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory); // in case user start app in a different working dictionary.

            //string[] pargs = Environment.GetCommandLineArgs();

            var appDateFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), SystemConfig.AppName);
#if DEV
            SimpleLogHelper.WriteLogEnumLogLevel = SimpleLogHelper.EnumLogLevel.Debug;
            Shawn.Utils.ConsoleManager.Show();
#endif
            // BASE MODULES
            InitLog(appDateFolder);
            InitExceptionHandle();
            UnSetShortcutAutoStart(startupEvent);
            OnlyOneAppInstanceCheck();
            KillPutty();
            InitEvent();
            RemoteWindowPool.Init(Context);

            // UI
            bool isNewUser = InitSystemConfig(appDateFolder);
            InitMainWindow(isNewUser);
            InitLauncher();
            InitTaskTray();

            // Database
            var connStatus = Context.InitSqliteDb(SystemConfig.Instance.DataSecurity.DbPath);
            if (connStatus != EnumDbStatus.OK)
            {
                string error = connStatus.GetErrorInfo(SystemConfig.Instance.Language, SystemConfig.Instance.DataSecurity.DbPath);
                MessageBox.Show(error,
                    SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK,
                    MessageBoxImage.Error, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                Window.Vm.CmdGoSysOptionsPage.Execute(typeof(SystemConfigDataSecurity));
            }
            else
            {
                Context.AppData.ServerListUpdate();
            }




            if (!SystemConfig.Instance.General.AppStartMinimized
                || isNewUser)
            {
                ActivateWindow();
            }
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

        private void InitLauncher()
        {
            SearchBoxWindow = new SearchBoxWindow(Context);
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
                RemoteWindowPool.Instance?.Release();
            }
            finally
            {
                Environment.Exit(exitCode);
            }
        }
    }
}