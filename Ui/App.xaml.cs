using System;
using System.Linq;
using System.Media;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using _1RM.Service;
using _1RM.Utils.Tracing;
#if FOR_MICROSOFT_STORE_ONLY
#if DEV
using System.IO;
#endif
using Windows.ApplicationModel.Activation;
using _1RM.Utils;
#endif


namespace _1RM
{
    static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            var argss = args.ToList();
            AppInitHelper.Init();
#if FOR_MICROSOFT_STORE_ONLY
            // see: https://stackoverflow.com/questions/57755792/how-can-i-handle-file-activation-from-a-wpf-app-which-is-running-as-uwp
            try
            {
                var aea = Windows.ApplicationModel.AppInstance.GetActivatedEventArgs();
                if (aea?.Kind == ActivationKind.StartupTask)
                {
                    // ref: https://blogs.windows.com/windowsdeveloper/2017/08/01/configure-app-start-log/
                    // If your app is enabled for startup activation, you should handle this case in your
                    // App class by overriding the OnActivated method.Check the IActivatedEventArgs.Kind
                    // to see if it is ActivationKind.StartupTask, and if so, case the IActivatedEventArgs
                    // to a StartupTaskActivatedEventArgs.
                    argss.Add(AppStartupHelper.APP_START_MINIMIZED);
#if DEBUG
                    string kind = aea?.Kind.ToString() ?? "null";
                    if (File.Exists(@"D:\1remtoe_arg_Kind.txt")) kind = File.ReadAllText(@"D:\1remtoe_arg_Kind.txt") + "\r\n" + kind;
                    File.WriteAllText(@"D:\1remtoe_arg_Kind.txt", kind);
                    if (File.Exists(@"D:\1remtoe_arg_data.txt")) File.Delete(@"D:\1remtoe_arg_data.txt");
                    File.WriteAllText(@"D:\1remtoe_arg_data.txt", string.Join("\r\n", argss));
#endif
                }
            }
            catch (Exception e)
            {
                UnifyTracing.Error(e);
            }
#endif
            AppStartupHelper.Init(argss); // in this method, it will call Environment.Exit() if needed
            var application = new App();
            application.InitializeComponent();
            application.Run();
        }
    }

    public partial class App : Application
    {
        public static ResourceDictionary? ResourceDictionary { get; private set; } = null;

        protected override void OnStartup(StartupEventArgs e)
        {
            ResourceDictionary = this.Resources;
            base.OnStartup(e);

            // Normalize mouse wheel scroll delta for precision trackpad compatibility.
            // Precision touchpads on Windows 10/11 may send WM_MOUSEWHEEL with large accumulated
            // delta values (multiples of 120), causing ScrollViewers to scroll too far per gesture.
            // This handler caps the effective delta to one standard wheel tick (120) per event.
            EventManager.RegisterClassHandler(
                typeof(ScrollViewer),
                UIElement.PreviewMouseWheelEvent,
                new MouseWheelEventHandler(NormalizeScrollViewerMouseWheel),
                true);

            // First, make a sound (one second of silence) in the main window
            // so that the Volume Mixer and others will recognize 1Remote as
            // an application that outputs sound.
            //
            // Otherwise, 1Remote is only be detected as a sound application
            // when an RDP session is started. However, it seemed odd that it
            // remained in this state even after all RDP sessions were
            // terminated.
            //
            // So while this application is running, from start to finish,
            // it's better to be visible as a sound application in the Volume
            // Mixer and others.
            try
            {
                var sri = Application.GetResourceStream(new Uri("pack://application:,,,/Resources/dummy.wav"));
                if (sri != null)
                {
                    using var s = sri.Stream;
                    System.Media.SoundPlayer player = new System.Media.SoundPlayer(s);
                    player.Load();
                    player.Play();
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        /// <summary>
        /// Normalizes the mouse wheel delta for all ScrollViewers to prevent over-scrolling
        /// when using precision trackpads that may send large accumulated delta values.
        /// </summary>
        private static readonly ConditionalWeakTable<ScrollViewer, ScrollWheelState> _scrollWheelStates = new();

        private sealed class ScrollWheelState
        {
            public int LastPassedTimestamp;
        }

        private static void NormalizeScrollViewerMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled) return;
            if (sender is not ScrollViewer scrollViewer) return;

            var state = _scrollWheelStates.GetOrCreateValue(scrollViewer);
            var timeDelta = e.Timestamp - state.LastPassedTimestamp;

            // Standard Windows WHEEL_DELTA: the delta for one physical mouse-wheel notch.
            const int wheelDelta = 120;
            // Minimum interval (ms) between events forwarded to the ScrollViewer.
            // Events arriving faster than this threshold are rate-limited so that precision
            // trackpads, which fire many rapid WM_MOUSEWHEEL messages, do not scroll too far.
            const int minIntervalMs = 100;

            bool isRapidFire = timeDelta >= 0 && timeDelta < minIntervalMs;
            bool isLargeDelta = Math.Abs(e.Delta) > wheelDelta;

            if (!isRapidFire && !isLargeDelta)
            {
                // Normal mouse-wheel event: record the timestamp and let WPF handle it.
                state.LastPassedTimestamp = e.Timestamp;
                return;
            }

            // Either the events are arriving too fast or the delta is unusually large.
            // Take control of scrolling so the amount stays predictable.
            e.Handled = true;

            if (isRapidFire)
            {
                // Rate limit exceeded: drop this event.
                return;
            }

            // Large delta (e.g. several ticks accumulated by the touchpad driver):
            // normalize to exactly one standard tick and re-dispatch on the ScrollViewer.
            // The re-dispatched event is MouseWheelEvent (bubble), not PreviewMouseWheelEvent
            // (tunnel), so it won't re-enter this class handler.
            state.LastPassedTimestamp = e.Timestamp;
            var normalizedDelta = Math.Sign(e.Delta) * wheelDelta;
            var normalizedEvent = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, normalizedDelta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = e.Source,
            };
            scrollViewer.RaiseEvent(normalizedEvent);
        }

        public static bool ExitingFlag = false;
        public static void Close(int exitCode = 0)
        {
            // workaround
            Task.Factory.StartNew(() =>
            {
                Thread.Sleep(5 * 1000);
                Environment.Exit(1);
            });
            ExitingFlag = true;
            Application.Current.Dispatcher.Invoke(() =>
            {
                Application.Current.Shutdown(exitCode);
            });
        }
    }
}
