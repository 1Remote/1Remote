using System;
using System.IO;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using PRM.Core.DB;
using PRM.Core.External.KiTTY;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.Protocol.Putty;
using PRM.Core.Service;
using Shawn.Utils;
using PRM.Model;
using PRM.View;
using PRM.View.ErrorReport;
using PRM.View.Guidance;
using PRM.View.ProtocolHosts;

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

        public static MainWindow MainUi { get; private set; } = null;
        public static SearchBoxWindow SearchBoxWindow { get; private set; } = null;
        public static PrmContext Context { get; private set; }
        public static System.Windows.Forms.NotifyIcon TaskTrayIcon { get; private set; } = null;
        private DesktopResolutionWatcher _desktopResolutionWatcher;
        public static bool CanPortable { get; private set; }

        private void InitLog(string appDateFolder)
        {
            SimpleLogHelper.WriteLogEnumLogLevel = SimpleLogHelper.EnumLogLevel.Warning;
            SimpleLogHelper.PrintLogEnumLogLevel = SimpleLogHelper.EnumLogLevel.Debug;
            // init log file placement
            var logFilePath = Path.Combine(appDateFolder, "Logs", $"{ConfigurationService.AppName}.log.md");
            var fi = new FileInfo(logFilePath);
            if (!fi.Directory.Exists)
                fi.Directory.Create();
            SimpleLogHelper.LogFileName = logFilePath;
        }

        private static void OnUnhandledException(Exception e)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                SimpleLogHelper.Fatal(e);
                CloseAllWindow();
                var errorReport = new ErrorReportWindow(e);
                errorReport.ShowDialog();
                App.Close();
            });
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

        private void OnlyOneAppInstanceCheck()
        {
            _onlyOneAppInstanceHelper = new OnlyOneAppInstanceHelper(ConfigurationService.AppName);
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
                {
                    Dispatcher.Invoke(() =>
                    {
                        App.MainUi?.ActivateMe();
                    });
                }
            };
        }

        private void InitEvent()
        {
            // Event register
            _desktopResolutionWatcher = new DesktopResolutionWatcher();
            _desktopResolutionWatcher.OnDesktopResolutionChanged += () =>
            {
                GlobalEventHelper.OnScreenResolutionChanged?.Invoke();
                ReloadTaskTrayContextMenu();
            };
        }

        private void KillPutty()
        {
            var fi = new FileInfo(PuttyConnectableExtension.GetKittyExeFullName());
            // kill putty process
            foreach (var process in Process.GetProcessesByName(fi.Name.ToLower().Replace(".exe", "")))
            {
                process.Kill();
            }
        }

        private void InitMainWindow()
        {
            MainUi = new MainWindow(Context);
            MainWindow = MainUi;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
        }

        private void App_OnStartup(object sender, StartupEventArgs startupEvent)
        {
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory); // in case user start app in a different working dictionary.


            #region Check permissions
#if FOR_MICROSOFT_STORE_ONLY
            CanPortable = false;
#else
            CanPortable = true;
#endif

            var tmp = new ConfigurationService(true, null);
            var languageService = new LanguageService(this.Resources, CultureInfo.CurrentCulture.Name.ToLower());
            var dbDir = new FileInfo(tmp.Database.SqliteDatabasePath).Directory;
            if (IOPermissionHelper.HasWritePermissionOnDir(dbDir.FullName) == false)
            {
                MessageBox.Show(languageService.Translate("write permissions alert", dbDir.FullName), languageService.Translate("messagebox_title_warning"), MessageBoxButton.OK);
                Environment.Exit(1);
            }
            if (IOPermissionHelper.HasWritePermissionOnFile(tmp.JsonPath) == false)
            {
                MessageBox.Show($"We don't have write permissions for the `{tmp.JsonPath}` file!\r\nPlease try:\r\n1. `run as administrator`\r\n2. change file permissions \r\n3. move PRemoteM to another folder.", languageService.Translate("messagebox_title_warning"), MessageBoxButton.OK);
                Environment.Exit(1);
            }


            #endregion





#if DEV
            SimpleLogHelper.WriteLogEnumLogLevel = SimpleLogHelper.EnumLogLevel.Debug;
            ConsoleManager.Show();
#endif
            KillPutty();

            // BASE MODULES
            InitLog(CanPortable ? Environment.CurrentDirectory : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ConfigurationService.AppName));
            InitExceptionHandle();
            OnlyOneAppInstanceCheck();
            InitEvent();

            Context = new PrmContext(CanPortable, this.Resources);
            RemoteWindowPool.Init(Context);

            // UI
            // if cfg is not existed, then it would be a new user
            bool isNewUser = !File.Exists(Context.ConfigurationService.JsonPath);
            if (isNewUser)
            {
                var gw = new GuidanceWindow(Context);
                gw.ShowDialog();
            }
            InitMainWindow();
            InitLauncher();
            InitTaskTray();

            // init Database here, to show alert if db connection goes wrong.
            var connStatus = Context.InitSqliteDb(Context.ConfigurationService.Database.SqliteDatabasePath);
            if (connStatus != EnumDbStatus.OK)
            {
                string error = connStatus.GetErrorInfo(Context.LanguageService);
                MessageBox.Show(error, Context.LanguageService.Translate("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                MainUi.Vm.CmdGoSysOptionsPage.Execute("Data");
            }
            else
            {
                Context.AppData.ReloadServerList();
            }

            if (Context.ConfigurationService.General.AppStartMinimized == false
                || isNewUser)
            {
                MainUi.ActivateMe();
            }
        }

        private static void InitTaskTray()
        {
            if (TaskTrayIcon != null) return;
            Debug.Assert(Application.GetResourceStream(new Uri("pack://application:,,,/LOGO.ico"))?.Stream != null);
            TaskTrayIcon = new System.Windows.Forms.NotifyIcon
            {
                Text = ConfigurationService.AppName,
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
                    MainUi.ActivateMe();
                }
            };
        }

        public static void ReloadTaskTrayContextMenu()
        {
            // rebuild TaskTrayContextMenu while language changed
            if (TaskTrayIcon == null) return;

            var title = new System.Windows.Forms.MenuItem(ConfigurationService.AppName);
            title.Click += (sender, args) =>
            {
                System.Diagnostics.Process.Start("https://github.com/VShawn/PRemoteM");
            };
            var @break = new System.Windows.Forms.MenuItem("-");
            var linkHowToUse = new System.Windows.Forms.MenuItem(Context.LanguageService.Translate("about_page_how_to_use"));
            linkHowToUse.Click += (sender, args) =>
            {
                System.Diagnostics.Process.Start("https://github.com/VShawn/PRemoteM/wiki");
            };
            var linkFeedback = new System.Windows.Forms.MenuItem(Context.LanguageService.Translate("about_page_feedback"));
            linkFeedback.Click += (sender, args) =>
            {
                System.Diagnostics.Process.Start("https://github.com/VShawn/PRemoteM/issues");
            };
            var exit = new System.Windows.Forms.MenuItem(Context.LanguageService.Translate("Exit"));
            exit.Click += (sender, args) => App.Close();
            var child = new System.Windows.Forms.MenuItem[] { title, @break, linkHowToUse, linkFeedback, exit };
            TaskTrayIcon.ContextMenu = new System.Windows.Forms.ContextMenu(child);
        }

        private void InitLauncher()
        {
            SearchBoxWindow = new SearchBoxWindow(Context);
            SearchBoxWindow.SetHotKey();
        }

        private static void CloseAllWindow()
        {
            try
            {
                App.MainUi?.Hide();
                App.MainUi?.Close();
                App.MainUi = null;
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
            }
        }

        public static void Close(int exitCode = 0)
        {
            CloseAllWindow();
            Environment.Exit(exitCode);
        }
    }
}