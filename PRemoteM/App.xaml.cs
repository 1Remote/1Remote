using System;
using System.IO;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Shawn.Utils;
using PRM.Model;
using PRM.Model.DAO;
using PRM.Model.DAO.Dapper;
using PRM.Service;
using PRM.Utils.KiTTY;
using PRM.View;
using PRM.View.ErrorReport;
using PRM.View.Guidance;
using PRM.View.Settings;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.FileSystem;

namespace PRM
{
    /*
    Defines:
        FOR_MICROSOFT_STORE_ONLY        =>  Disable all functions store not recommend.Must define FOR_MICROSOFT_STORE first!!!
    */

    public partial class App : Application
    {
        public static LanguageService LanguageService { get; private set; }
        private NamedPipeHelper _namedPipeHelper;
        public static MainWindowView MainUi { get; private set; } = null;
        public static SettingsPageViewModel SettingsPageVm { get; private set; } = null;
        public static LauncherWindow LauncherWindow { get; private set; } = null;
        public static PrmContext Context { get; private set; }
        public static System.Windows.Forms.NotifyIcon TaskTrayIcon { get; private set; } = null;
        private DesktopResolutionWatcher _desktopResolutionWatcher;
        public bool CanPortable { get; private set; }

        public static Dispatcher UiDispatcher = null;

        private void InitLog()
        {
            var baseDir = CanPortable ? Environment.CurrentDirectory : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ConfigurationService.AppName);

            SimpleLogHelper.WriteLogLevel = SimpleLogHelper.EnumLogLevel.Warning;
            SimpleLogHelper.PrintLogLevel = SimpleLogHelper.EnumLogLevel.Debug;
            // init log file placement
            var logFilePath = Path.Combine(baseDir, "Logs", $"{ConfigurationService.AppName}.log.md");
            var fi = new FileInfo(logFilePath);
            if (!fi.Directory.Exists)
                fi.Directory.Create();
            SimpleLogHelper.LogFileName = logFilePath;

            // old version log files cleanup
            if (CanPortable)
            {
                var diLogs = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ConfigurationService.AppName, "Logs"));
                if (diLogs.Exists)
                    diLogs.Delete(true);
                var diApp = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ConfigurationService.AppName));
                if (diApp.Exists)
                {
                    var fis = diApp.GetFiles("*.md");
                    foreach (var info in fis)
                    {
                        info.Delete();
                    }
                }
            }
        }

        private static void OnUnhandledException(Exception e)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                lock (App.Current)
                {
                    SimpleLogHelper.Fatal(e);
                    var errorReport = new ErrorReportWindow(e);
                    errorReport.ShowDialog();
#if FOR_MICROSOFT_STORE_ONLY
                    throw e;
#else
                    CloseAllWindow();
                    App.Close();
#endif 
                }
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
#if FOR_MICROSOFT_STORE_ONLY
            string instanceName = ConfigurationService.AppName + "_Store_" + MD5Helper.GetMd5Hash16BitString(Environment.UserName);
#else
            string instanceName = ConfigurationService.AppName + "_" + MD5Helper.GetMd5Hash16BitString(Environment.CurrentDirectory + Environment.UserName);
#endif
            _namedPipeHelper = new NamedPipeHelper(instanceName);
            if (_namedPipeHelper.IsServer == false)
            {
                try
                {
                    _namedPipeHelper.NamedPipeSendMessage("ActivateMe");
                    Environment.Exit(0);
                }
                catch (Exception e)
                {
                    SimpleLogHelper.Fatal(e);
                    Environment.Exit(0);
                }
            }

            _namedPipeHelper.OnMessageReceived += message =>
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

        private void InitMainWindow(SettingsPageViewModel c)
        {
            MainUi = new MainWindowView(Context, c);
            MainWindow = MainUi;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory); // in case user start app in a different working dictionary.

            #region Check permissions
#if FOR_MICROSOFT_STORE_ONLY
            CanPortable = false;
#else
            CanPortable = true;
#endif

            var tmp = new ConfigurationService(CanPortable, null);
            var languageService = new LanguageService(this.Resources, CultureInfo.CurrentCulture.Name.ToLower());
            var dbDir = new FileInfo(tmp.Database.SqliteDatabasePath).Directory;
            if (IoPermissionHelper.HasWritePermissionOnDir(dbDir.FullName) == false)
            {
                MessageBox.Show(languageService.Translate("write permissions alert", dbDir.FullName), languageService.Translate("messagebox_title_warning"), MessageBoxButton.OK);
                Environment.Exit(1);
            }
            if (IoPermissionHelper.HasWritePermissionOnFile(tmp.JsonPath) == false)
            {
                MessageBox.Show($"We don't have write permissions for the `{tmp.JsonPath}` file!\r\nPlease try:\r\n1. `run as administrator`\r\n2. change file permissions \r\n3. move PRemoteM to another folder.", languageService.Translate("messagebox_title_warning"), MessageBoxButton.OK);
                Environment.Exit(1);
            }


            #endregion

            InitLog();

#if DEV
            SimpleLogHelper.WriteLogLevel = SimpleLogHelper.EnumLogLevel.Debug;
            ConsoleManager.Show();
#endif
            KillPutty();

            // BASE MODULES
            InitExceptionHandle();
            OnlyOneAppInstanceCheck();
            InitEvent();

            Context = new PrmContext(CanPortable, this.Resources);
            LanguageService = Context.LanguageService;
            RemoteWindowPool.Init(Context);
            // UI
            // if cfg is not existed, then it would be a new user
            bool isNewUser = !File.Exists(Context.ConfigurationService.JsonPath);
            if (isNewUser)
            {
                var gw = new GuidanceWindow(Context);
                gw.ShowDialog();
            }

            // init Database here, to show alert if db connection goes wrong.
            var connStatus = Context.InitSqliteDb();

            SettingsPageViewModel.Init(App.Context);
            App.SettingsPageVm = SettingsPageViewModel.GetInstance();
            InitMainWindow(App.SettingsPageVm);
            InitLauncher();
            InitTaskTray();


            if (connStatus != EnumDbStatus.OK)
            {
                string error = connStatus.GetErrorInfo(Context.LanguageService);
                MessageBox.Show(error, Context.LanguageService.Translate("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                MainUi.Vm.CmdGoSysOptionsPage.Execute("Data");
                MainUi.ActivateMe();
            }
            else
            {
                Context.AppData.ReloadServerList();
                SetDbWatcher();
                if (Context.DataService.DB() is DapperDataBaseFree)
                {
                    SettingsPageVm.PropertyChanged += (sender, args) =>
                    {
                        if (args.PropertyName == nameof(SettingsPageViewModel.DbPath))
                        {
                            SetDbWatcher();
                        }
                    };
                }
            }
            if (Context.ConfigurationService.General.AppStartMinimized == false
                || isNewUser)
            {
                MainUi.ActivateMe();
            }

            base.OnStartup(e);
        }

        //private FileWatcher _dbFileWatcher;
        private void SetDbWatcher()
        {
            // 以下代码会在自己更新数据库时同时被激活，当进行循环批量修改时，每修改一条记录都会重新读取一次数据库，会导致数据库连接冲突
            //_dbFileWatcher?.Dispose();
            //var dbfi = new FileInfo(SettingsPageVm.DbPath);
            //if (dbfi.Exists)
            //{
            //    _dbFileWatcher = new FileWatcher(FileWatcherMode.ContentChanged, dbfi.Directory.FullName, TimeSpan.FromMilliseconds(300), "*.db");
            //    _dbFileWatcher.PathChanged += (sender, args) =>
            //    {
            //        var fi = new FileInfo(args.Path);
            //        if (fi.FullName == dbfi.FullName)
            //        {
            //            Task.Factory.StartNew(() =>
            //            {
            //                for (int i = 0; i < 20; i++)
            //                {
            //                    if (IoPermissionHelper.HasWritePermissionOnFile(dbfi.FullName))
            //                    {
            //                        Context.InitSqliteDb(fi.FullName);
            //                        App.UiDispatcher?.Invoke(() =>
            //                        {
            //                            Context.AppData.ReloadServerList();
            //                        });
            //                        return;
            //                    }
            //                    Thread.Sleep(100);
            //                }
            //            });
            //        }
            //    };
            //}
        }
        private static void InitTaskTray()
        {
            if (TaskTrayIcon != null) return;
            Debug.Assert(Application.GetResourceStream(ResourceUriHelper.GetUriFromCurrentAssembly("LOGO.ico"))?.Stream != null);
            TaskTrayIcon = new System.Windows.Forms.NotifyIcon
            {
                Text = ConfigurationService.AppName,
                Icon = new System.Drawing.Icon(Application.GetResourceStream(ResourceUriHelper.GetUriFromCurrentAssembly("LOGO.ico")).Stream),
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
            LauncherWindow = new LauncherWindow(Context);
            LauncherWindow.SetHotKey();
        }

        private static void CloseAllWindow()
        {
            try
            {
                App.LauncherWindow?.Hide();
                App.LauncherWindow?.Close();
                App.LauncherWindow = null;
                App.MainUi?.Hide();
                App.MainUi?.Close();
                App.MainUi = null;

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