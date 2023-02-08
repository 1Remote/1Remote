using System;
using System.Windows;
using System.Windows.Threading;
using _1RM.Model;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Utils;
using _1RM.View;
using _1RM.View.Editor;
using _1RM.View.ErrorReport;
using _1RM.View.Launcher;
using _1RM.View.Settings;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Stylet;
using StyletIoC;
using MessageBoxViewModel = _1RM.View.Utils.MessageBoxViewModel;
using ServerListPageViewModel = _1RM.View.ServerList.ServerListPageViewModel;

namespace _1RM
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
            // Configure the IoC container in here;
            builder.Bind<ILanguageService>().And<LanguageService>().ToInstance(_appInit.LanguageService);
            builder.Bind<TaskTrayService>().ToSelf().InSingletonScope();
            builder.Bind<LocalityService>().ToSelf().InSingletonScope();
            builder.Bind<KeywordMatchService>().ToInstance(_appInit.KeywordMatchService);
            builder.Bind<Configuration>().ToInstance(_appInit.NewConfiguration);
            builder.Bind<ConfigurationService>().ToInstance(_appInit.ConfigurationService);
            builder.Bind<ThemeService>().ToInstance(_appInit.ThemeService);
            builder.Bind<GlobalData>().ToInstance(_appInit.GlobalData);
            builder.Bind<ProtocolConfigurationService>().ToSelf().InSingletonScope();
            builder.Bind<DataSourceService>().ToSelf().InSingletonScope();
            builder.Bind<LauncherService>().ToSelf().InSingletonScope();

            builder.Bind<MainWindowView>().ToSelf().InSingletonScope();
            builder.Bind<MainWindowViewModel>().ToSelf().InSingletonScope();
            builder.Bind<LauncherWindowView>().ToSelf().InSingletonScope();
            builder.Bind<LauncherWindowViewModel>().ToSelf().InSingletonScope();
            builder.Bind<ServerSelectionsViewModel>().ToSelf().InSingletonScope();
            builder.Bind<QuickConnectionViewModel>().ToSelf().InSingletonScope();
            builder.Bind<AboutPageViewModel>().ToSelf().InSingletonScope();
            builder.Bind<SettingsPageViewModel>().ToSelf().InSingletonScope();
            builder.Bind<ServerListPageViewModel>().ToSelf().InSingletonScope();
            builder.Bind<ProcessingRingViewModel>().ToSelf().InSingletonScope();
            builder.Bind<RequestRatingViewModel>().ToSelf();
            builder.Bind<ServerEditorPageViewModel>().ToSelf();
            builder.Bind<SessionControlService>().ToSelf().InSingletonScope();

            builder.Bind<IMessageBoxViewModel>().To<MessageBoxViewModel>();
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
            MsAppCenterHelper.TraceSpecial("Distributor", $"{Assert.APP_NAME} MS Store");
#else
            MsAppCenterHelper.TraceAppStatus(true, false);
            MsAppCenterHelper.TraceSpecial("Distributor", $"{Assert.APP_NAME} Exe");
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
            if (!App.ExitingFlag)
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
            e.Handled = true;
        }
    }
}
