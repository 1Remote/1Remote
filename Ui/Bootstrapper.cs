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
        private readonly AppInit _appInit = new AppInit();
        protected override void OnStart()
        {
            // Step1
            // This is called just after the application is started, but before the IoC container is set up.
            // Set up things like logging, etc

            _appInit.InitOnStart();
        }

        protected override void ConfigureIoC(IStyletIoCBuilder builder)
        {
            // Step2
            // Configure the IoC container in here
            builder.Bind<IDataService>().And<DataService>().To<DataService>();
            builder.Bind<ILanguageService>().And<LanguageService>().ToInstance(_appInit.LanguageService);
            builder.Bind<LocalityService>().ToSelf().InSingletonScope();
            builder.Bind<KeywordMatchService>().ToInstance(_appInit.KeywordMatchService);
            builder.Bind<Configuration>().ToInstance(_appInit.Configuration);
            builder.Bind<ConfigurationService>().ToInstance(_appInit.ConfigurationService);
            builder.Bind<ThemeService>().ToInstance(_appInit.ThemeService);
            builder.Bind<GlobalData>().ToInstance(_appInit.GlobalData);
            builder.Bind<ProtocolConfigurationService>().ToSelf().InSingletonScope();
            builder.Bind<PrmContext>().ToSelf().InSingletonScope();

            builder.Bind<MainWindowView>().ToSelf().InSingletonScope();
            builder.Bind<MainWindowViewModel>().ToSelf().InSingletonScope();
            builder.Bind<LauncherWindowView>().ToSelf().InSingletonScope();
            builder.Bind<LauncherWindowViewModel>().ToSelf().InSingletonScope();
            builder.Bind<AboutPageViewModel>().ToSelf().InSingletonScope();
            builder.Bind<SettingsPageViewModel>().ToSelf();
            builder.Bind<ServerListPageViewModel>().ToSelf();
            builder.Bind<ProcessingRingViewModel>().ToSelf();
            builder.Bind<RequestRatingViewModel>().ToSelf();
            builder.Bind<ServerEditorPageViewModel>().ToSelf();
            builder.Bind<SessionControlService>().ToSelf().InSingletonScope();
            base.ConfigureIoC(builder);
        }



        protected override void Configure()
        {
            // Step3
            // This is called after Stylet has created the IoC container, so this.Container exists, but before the
            // Root ViewModel is launched.
            // Configure your services, etc, in here
            IoC.Init(this.Container);
            _appInit.InitOnConfigure();
        }

        protected override void OnLaunch()
        {
            // Step4
            // This is called just after the root ViewModel has been launched
            // Something like a version check that displays a dialog might be launched from here


            // init Database here after ui init, to show alert if db connection goes wrong.
            _appInit.InitOnLaunch();
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
                App.Close(100);
#endif
            }
        }
    }
}
