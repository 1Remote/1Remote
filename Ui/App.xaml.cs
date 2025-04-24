using System;
using System.Linq;
using System.Media;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using _1RM.Service;
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
                }
            }
            catch (Exception e)
            {
                SentryIoHelper.Error(e);
            }
#if DEV
            string kind = aea?.Kind.ToString() ?? "null";
            if (File.Exists(@"D:\1remtoe_arg_Kind.txt")) kind = File.ReadAllText(@"D:\1remtoe_arg_Kind.txt") + "\r\n" + kind;
            File.WriteAllText(@"D:\1remtoe_arg_Kind.txt", kind);
            if (File.Exists(@"D:\1remtoe_arg_data.txt")) File.Delete(@"D:\1remtoe_arg_data.txt");
            File.WriteAllText(@"D:\1remtoe_arg_data.txt", string.Join("\r\n", argss));
#endif
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
