using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using PRM.Model;
using PRM.Model.DAO;
using PRM.Model.DAO.Dapper;
using PRM.Service;
using PRM.View;
using PRM.View.Editor;
using PRM.View.ErrorReport;
using PRM.View.Guidance;
using PRM.View.Settings;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf.FileSystem;
using Stylet;
using StyletIoC;
using Ui;

namespace PRM
{
    public class Bootstrapper : Bootstrapper<MainWindowViewModel>
    {
        private bool _canPortable = false;
        private bool _isNewUser = false;
        private string _baseFolder = "";

        private KeywordMatchService? _keywordMatchService;
        private ConfigurationService? _configurationService;
        private ThemeService? _themeService;
        private GlobalData? _globalData;

        protected override void OnStart()
        {
            // Step1
            // This is called just after the application is started, but before the IoC container is set up.
            // Set up things like logging, etc

            // TODO 确定当前软件是 Portable 模式还是多用户模式 

            #region Check permissions
#if FOR_MICROSOFT_STORE_ONLY
            _canPortable = false;
#else
            _canPortable = true;
#endif

            // Check for permissions based on CanPortable
            var tmp = new ConfigurationService(_canPortable, null);
            var dbDir = new FileInfo(tmp.Database.SqliteDatabasePath).Directory;
            Debug.Assert(dbDir != null);
            {
                var languageService = new LanguageService(App.ResourceDictionary);
                languageService.SetLanguage(CultureInfo.CurrentCulture.Name.ToLower());
                if (IoPermissionHelper.HasWritePermissionOnDir(dbDir.FullName) == false)
                {
                    MessageBox.Show(languageService.Translate("write permissions alert", dbDir.FullName), languageService.Translate("messagebox_title_warning"), MessageBoxButton.OK);
                    Environment.Exit(1);
                }

                if (IoPermissionHelper.HasWritePermissionOnFile(tmp.JsonPath) == false)
                {
                    MessageBox.Show($"We don't have write permissions for the `{tmp.JsonPath}` file!\r\nPlease try:\r\n1. `run as administrator`\r\n2. change file permissions \r\n3. move PRemoteM to another folder.",
                        languageService.Translate("messagebox_title_warning"), MessageBoxButton.OK);
                    Environment.Exit(1);
                }
            }

            #endregion

            _baseFolder = _canPortable ? Environment.CurrentDirectory : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ConfigurationService.AppName);

            AppInit.InitLog(_canPortable);
            AppInit.OnlyOneAppInstanceCheck();


            _keywordMatchService = new KeywordMatchService();
            _configurationService = new ConfigurationService(_canPortable, _keywordMatchService);
            _themeService = new ThemeService(App.ResourceDictionary, _configurationService.Theme);
            _globalData = new GlobalData(_configurationService);
        }

        protected override void ConfigureIoC(IStyletIoCBuilder builder)
        {
            // Step2
            // Configure the IoC container in here
            builder.Bind<IDataService>().And<DataService>().To<DataService>();
            builder.Bind<ILanguageService>().And<LanguageService>().ToInstance(new LanguageService(App.ResourceDictionary));
            builder.Bind<LocalityService>().ToInstance(new LocalityService(new Ini(Path.Combine(_baseFolder, "locality.ini"))));
            builder.Bind<KeywordMatchService>().ToInstance(_keywordMatchService);
            builder.Bind<ConfigurationService>().ToInstance(_configurationService);
            builder.Bind<ThemeService>().ToInstance(_themeService);
            builder.Bind<GlobalData>().ToInstance(_globalData);
            builder.Bind<ProtocolConfigurationService>().ToInstance(new ProtocolConfigurationService(_canPortable));
            builder.Bind<PrmContext>().ToSelf().InSingletonScope();

            builder.Bind<MainWindowView>().ToSelf().InSingletonScope();
            builder.Bind<MainWindowViewModel>().ToSelf().InSingletonScope();
            builder.Bind<LauncherWindowView>().ToSelf().InSingletonScope();
            builder.Bind<LauncherWindowViewModel>().ToSelf().InSingletonScope();
            builder.Bind<AboutPageViewModel>().ToSelf().InSingletonScope();
            builder.Bind<SettingsPageViewModel>().ToSelf().InSingletonScope();
            builder.Bind<ServerListPageViewModel>().ToSelf();
            builder.Bind<ProcessingRingViewModel>().ToSelf();
            builder.Bind<RequestRatingViewModel>().ToSelf();
            builder.Bind<ServerEditorPageViewModel>().ToSelf();
            builder.Bind<GuidanceWindow>().ToSelf();
            builder.Bind<RemoteWindowPool>().ToSelf().InSingletonScope();
            base.ConfigureIoC(builder);
        }



        private EnumDbStatus _dbConnectionStatus;
        protected override void Configure()
        {
            // Step3
            // This is called after Stylet has created the IoC container, so this.Container exists, but before the
            // Root ViewModel is launched.
            // Configure your services, etc, in here

            IoC.Init(this.Container);
            IoC.Get<LanguageService>().SetLanguage(IoC.Get<ConfigurationService>().General.CurrentLanguageCode);
            var context = IoC.Get<PrmContext>();
            context.Init(_canPortable);
            IoC.Get<RemoteWindowPool>();
            _dbConnectionStatus = context.InitSqliteDb();
            IoC.Get<GlobalData>().ReloadServerList();

            // if cfg is not existed, then it would be a new user
            _isNewUser = !File.Exists(context.ConfigurationService.JsonPath);
            if (_isNewUser)
            {
                var gw = IoC.Get<GuidanceWindow>();
                gw.ShowDialog();
            }
        }

        protected override void OnLaunch()
        {
            // Step4
            // This is called just after the root ViewModel has been launched
            // Something like a version check that displays a dialog might be launched from here


            // init Database here after ui init, to show alert if db connection goes wrong.
            if (_dbConnectionStatus != EnumDbStatus.OK)
            {
                string error = _dbConnectionStatus.GetErrorInfo();
                MessageBox.Show(error, IoC.Get<LanguageService>().Translate("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                IoC.Get<MainWindowViewModel>().CmdGoSysOptionsPage.Execute("Data");
                IoC.Get<MainWindowViewModel>().ActivateMe();
            }


            if (IoC.Get<ConfigurationService>().General.AppStartMinimized == false
                || _isNewUser)
            {
                IoC.Get<MainWindowViewModel>().ActivateMe();
            }


            base.OnLaunch();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Called on Application.Exit
        }

        protected override void OnUnhandledException(DispatcherUnhandledExceptionEventArgs e)
        {
            lock (this)
            {
                SimpleLogHelper.Fatal(e.Exception);
                var errorReport = new ErrorReportWindow(e.Exception);
                errorReport.ShowDialog();
#if FOR_MICROSOFT_STORE_ONLY
                    throw e.Exception;
#else
                Environment.Exit(100);
#endif
            }
        }
    }
}
