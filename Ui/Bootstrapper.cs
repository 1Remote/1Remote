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
            // Check if this is a transient GDI+ error from WindowsFormsHost
            // These errors are common in Windows 11 24H2 during window switching and can be safely ignored
            // See: https://github.com/1Remote/1Remote/issues/924
            if (IsTransientGdiError(e.Exception))
            {
                SimpleLogHelper.Warning($"Transient GDI+ error suppressed: {e.Exception.Message}");
                e.Handled = true;
                return;
            }

            // Check if this is a transient "Desktop composition is disabled" error from WPF's
            // WindowChromeWorker and ignore it if so.
            // During RDP session lock/reconnect, GPU driver resets or display topology changes,
            // DWM composition is transiently unavailable: WPF reacts to WM_DWMCOMPOSITIONCHANGED
            // by re-applying the glass frame, but DwmExtendFrameIntoClientArea then throws
            // COMException 0x80263001 (DWM_E_COMPOSITIONDISABLED) from inside a window message hook.
            // The failed call is purely cosmetic - the glass frame is re-applied automatically when
            // the next WM_DWMCOMPOSITIONCHANGED arrives after composition settles.
            // See: https://github.com/dotnet/wpf/issues/11440
            if (IsTransientDwmCompositionError(e.Exception))
            {
                SimpleLogHelper.Warning($"Transient DWM composition error suppressed: {e.Exception.Message}");
                e.Handled = true;
                return;
            }

            if (!App.ExitingFlag)
            {
                // Capture exception reference inside lock to prevent concurrent modifications
                Exception? exceptionToHandle = null;
                lock (this)
                {
                    exceptionToHandle = e.Exception;
                }

                // Log and handle UI operations outside of lock
                if (exceptionToHandle != null)
                {
                    SimpleLogHelper.Fatal(exceptionToHandle);
                    UnifyTracing.Error(exceptionToHandle, new Dictionary<string, string>()
                    {
                        {"Where", "Bootstrapper.OnUnhandledException"},
                    });

                    Execute.OnUIThread(() =>
                    {
                        if (!App.ExitingFlag)
                            try
                            {
                                var errorReport = new ErrorReportWindow(exceptionToHandle);
                                errorReport.ShowDialog();
                            }
                            finally
                            {
                                App.Close(100);
                            }
                    });
                }
            }
            e.Handled = true;
        }

        /// <summary>
        /// Checks if the exception is a transient GDI+ error that can be safely ignored.
        /// These errors typically occur during rapid window switching or painting operations
        /// in WindowsFormsHost controls on Windows 11 24H2.
        /// </summary>
        private static bool IsTransientGdiError(Exception ex)
        {
            // Check for System.Runtime.InteropServices.ExternalException with GDI+ error code
            if (ex is System.Runtime.InteropServices.ExternalException externalEx)
            {
                // HRESULT 0x80004005 is E_FAIL, commonly used for GDI+ errors
                // The specific error message "A generic error occurred in GDI+." indicates a transient painting issue
                if (externalEx.ErrorCode == unchecked((int)0x80004005) && 
                    (ex.Message?.Contains("GDI+", StringComparison.OrdinalIgnoreCase) == true ||
                     ex.Message?.Contains("generic error", StringComparison.OrdinalIgnoreCase) == true))
                {
                    // Additionally check if the stack trace involves painting operations in WindowsFormsHost
                    var stackTrace = ex.StackTrace ?? "";
                    if (stackTrace.Contains("PaintBackground", StringComparison.Ordinal) ||
                        stackTrace.Contains("WinFormsAdapter", StringComparison.Ordinal) ||
                        stackTrace.Contains("Graphics.FillRectangle", StringComparison.Ordinal) ||
                        stackTrace.Contains("Graphics.CheckErrorStatus", StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if the exception is a transient DWM composition error that can be safely ignored.
        /// These errors occur when WPF's WindowChromeWorker handles WM_DWMCOMPOSITIONCHANGED during
        /// session transitions (RDP lock/reconnect, GPU driver reset, display change): DwmIsCompositionEnabled()
        /// reports composition as enabled, but DwmExtendFrameIntoClientArea fails with
        /// 0x80263001 (DWM_E_COMPOSITIONDISABLED) moments later. The failure only skips re-applying
        /// the glass frame for that message and is retried on the next composition change.
        /// See: https://github.com/dotnet/wpf/issues/11440
        /// </summary>
        private static bool IsTransientDwmCompositionError(Exception ex)
        {
            // DWM_E_COMPOSITIONDISABLED: "{Desktop composition is disabled}"
            const int DWM_E_COMPOSITIONDISABLED = unchecked((int)0x80263001);

            if (ex is System.Runtime.InteropServices.COMException comEx &&
                comEx.ErrorCode == DWM_E_COMPOSITIONDISABLED)
            {
                // Only swallow failures raised from WPF's chrome handling, never from app code
                var stackTrace = ex.StackTrace ?? "";
                return stackTrace.Contains("WindowChromeWorker", StringComparison.Ordinal) ||
                       stackTrace.Contains("DwmExtendFrameIntoClientArea", StringComparison.Ordinal);
            }

            return false;
        }
    }
}
