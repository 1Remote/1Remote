using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using _1RM.Model;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Service.Locality;
using _1RM.Utils.Tracing;
using _1RM.View;
using _1RM.View.ErrorReport;
using _1RM.View.Launcher;
using _1RM.View.Settings;
using _1RM.View.Settings.CredentialVault;
using _1RM.View.Settings.DataSource;
using _1RM.View.Settings.General;
using _1RM.View.Settings.Launcher;
using _1RM.View.Settings.ProtocolConfig;
using _1RM.View.Settings.Theme;
using _1RM.View.Utils;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Stylet;
using StyletIoC;
using MessageBoxViewModel = _1RM.View.Utils.MessageBoxViewModel;
using ServerListPageViewModel = _1RM.View.ServerView.ServerListPageViewModel;
using ServerTreeViewModel = _1RM.View.ServerView.Tree.ServerTreeViewModel;

namespace _1RM
{
    public class Bootstrapper : Bootstrapper<LauncherWindowViewModel>
    {
        private readonly DesktopResolutionWatcher _desktopResolutionWatcher = new();

        protected override void OnStart()
        {
            // Step1
            // This is called just after the application is started, but before the IoC container is set up.
            // Set up things like logging, etc
            AppInitHelper.InitOnStart();
        }

        protected override void ConfigureIoC(IStyletIoCBuilder builder)
        {
            // Step2
            // Configure the IoC container in here;
            builder.Bind<ILanguageService>().And<LanguageService>().ToInstance(AppInitHelper.LanguageServiceObj);
            builder.Bind<TaskTrayService>().ToSelf().InSingletonScope();
            builder.Bind<LocalityService>().ToSelf().InSingletonScope();
            builder.Bind<KeywordMatchService>().ToInstance(AppInitHelper.KeywordMatchServiceObj);
            builder.Bind<ConfigurationService>().ToInstance(AppInitHelper.ConfigurationServiceObj);
            builder.Bind<ThemeService>().ToInstance(AppInitHelper.ThemeServiceObj);
            builder.Bind<GlobalData>().ToInstance(AppInitHelper.GlobalDataObj);
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
            builder.Bind<GeneralSettingViewModel>().ToSelf().InSingletonScope();
            builder.Bind<DataSourceViewModel>().ToSelf().InSingletonScope();
            builder.Bind<CredentialVaultViewModel>().ToSelf().InSingletonScope();
            builder.Bind<LauncherSettingViewModel>().ToSelf().InSingletonScope();
            builder.Bind<ThemeSettingViewModel>().ToSelf().InSingletonScope();
            builder.Bind<ProtocolRunnerSettingsPageViewModel>().ToSelf().InSingletonScope();

            builder.Bind<ServerListPageViewModel>().ToSelf().InSingletonScope();
            builder.Bind<ServerTreeViewModel>().ToSelf().InSingletonScope();
            builder.Bind<SessionControlService>().ToSelf().InSingletonScope();

            builder.Bind<ProcessingRingViewModel>().ToSelf();
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
            AppInitHelper.InitOnConfigure();
            _desktopResolutionWatcher.OnDesktopResolutionChanged += () =>
            {
                GlobalEventHelper.OnScreenResolutionChanged?.Invoke();
                IoC.Get<TaskTrayService>().TaskTrayInit();
            };
        }

        protected override void OnLaunch()
        {
            // Step4
            // This is called just after the root ViewModel has been launched
            // Something like a version check that displays a dialog might be launched from here


            // init Database here after ui init, to show alert if db connection goes wrong.
            AppInitHelper.InitOnLaunch();
            IoC.Get<TaskTrayService>().TaskTrayInit();
        }


        protected override void OnExit(ExitEventArgs e)
        {
            // workaround
            Task.Factory.StartNew(() =>
            {
                Thread.Sleep(5 * 1000);
                Environment.Exit(1);
            });
            IoC.Get<TaskTrayService>().TaskTrayDispose();
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
                    UnifyTracing.Error(e.Exception, new Dictionary<string, string>()
                    {
                        {"Where", "Bootstrapper.OnUnhandledException"},
                    });
                    Execute.OnUIThread(() =>
                    {
                        if (!App.ExitingFlag)
                            try
                            {
                                var errorReport = new ErrorReportWindow(e.Exception);
                                errorReport.ShowDialog();
                            }
                            finally
                            {
                                App.Close(100);
                            }
                    });
                }
            e.Handled = true;
        }
    }
}
