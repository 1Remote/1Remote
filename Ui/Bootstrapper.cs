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
using PRM.Utils;
using PRM.View;
using PRM.View.Editor;
using PRM.View.ErrorReport;
using PRM.View.Guidance;
using PRM.View.Settings;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.FileSystem;
using Stylet;
using StyletIoC;
using Ui;

namespace PRM
{
    public class Bootstrapper : Bootstrapper<LauncherWindowViewModel>
    {
        private readonly AppInit _appInit = new();
        private readonly DesktopResolutionWatcher _desktopResolutionWatcher = new();

        public Bootstrapper()
        {
            OnlyOneAppInstanceCheck();
        }

        #region OnlyOneAppInstanceCheck
#if FOR_MICROSOFT_STORE_ONLY
        private readonly NamedPipeHelper _namedPipeHelper = new NamedPipeHelper(Assert.APP_NAME + "_Store_" + MD5Helper.GetMd5Hash16BitString(Environment.CurrentDirectory + Environment.UserName));
#else
        private readonly NamedPipeHelper _namedPipeHelper = new NamedPipeHelper(Assert.APP_NAME + "_" + MD5Helper.GetMd5Hash16BitString(Environment.CurrentDirectory + Environment.UserName));
#endif
        public void OnlyOneAppInstanceCheck()
        {
            if (_namedPipeHelper.IsServer == false)
            {
                try
                {
                    _namedPipeHelper.NamedPipeSendMessage("ActivateMe");
                    Environment.Exit(0);
                }
                catch
                {
                    Environment.Exit(1);
                }
            }
            else
            {
                _namedPipeHelper.OnMessageReceived += message =>
                {
                    SimpleLogHelper.Debug("NamedPipeServerStream get: " + message);
                    if (message == "ActivateMe")
                    {
                        IoC.Get<MainWindowViewModel>()?.ShowMe(true);
                    }
                };
            }
        }

        #endregion

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
            builder.Bind<TaskTrayService>().ToSelf().InSingletonScope();
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
            builder.Bind<SettingsPageViewModel>().ToSelf().InSingletonScope();
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
            _desktopResolutionWatcher.OnDesktopResolutionChanged += () =>
            {
                GlobalEventHelper.OnScreenResolutionChanged?.Invoke();
                IoC.Get<TaskTrayService>().TaskTrayInit();
            };
        }

        protected override void OnLaunch()
        {
#if FOR_MICROSOFT_STORE_ONLY
            MsAppCenterHelper.TraceAppStatus(true, true);
#else
            MsAppCenterHelper.TraceAppStatus(true, false);
#endif
            // Step4
            // This is called just after the root ViewModel has been launched
            // Something like a version check that displays a dialog might be launched from here


            // init Database here after ui init, to show alert if db connection goes wrong.
            _appInit.InitOnLaunch();
            IoC.Get<TaskTrayService>().TaskTrayInit();
        }


        protected override void OnExit(ExitEventArgs e)
        {
            IoC.Get<TaskTrayService>().TaskTrayDispose();
            _namedPipeHelper?.Dispose();
            IoC.Get<SessionControlService>()?.Release();
            if (IoC.Get<LauncherWindowViewModel>()?.View != null)
                IoC.Get<LauncherWindowViewModel>()?.RequestClose();
            if (IoC.Get<MainWindowViewModel>()?.View != null)
                IoC.Get<MainWindowViewModel>().RequestClose();
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
