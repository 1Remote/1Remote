using System;
using System.IO;
using System.Windows;
using PRM.Service;
using PRM.View;
using Shawn.Utils;
using Shawn.Utils.Wpf;

namespace Ui
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static NamedPipeHelper? _namedPipeHelper;
        public static void OnlyOneAppInstanceCheck()
        {
#if FOR_MICROSOFT_STORE_ONLY
            string instanceName = AppPathHelper.APP_NAME + "_Store_" + MD5Helper.GetMd5Hash16BitString(Environment.UserName);
#else
            string instanceName = AppPathHelper.APP_NAME + "_" + MD5Helper.GetMd5Hash16BitString(Environment.CurrentDirectory + Environment.UserName);
#endif
            _namedPipeHelper = new NamedPipeHelper(instanceName);
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

            _namedPipeHelper.OnMessageReceived += message =>
            {
                SimpleLogHelper.Debug("NamedPipeServerStream get: " + message);
                if (message == "ActivateMe")
                {
                    IoC.Get<MainWindowViewModel>()?.ActivateMe();
                }
            };
        }


        public static ResourceDictionary? ResourceDictionary { get; private set; } = null;

        protected override void OnStartup(StartupEventArgs e)
        {
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory); // in case user start app in a different working dictionary.
            ResourceDictionary = this.Resources;
            base.OnStartup(e);
        }

        public static void Close(int exitCode = 0)
        {
            _namedPipeHelper?.Dispose();
            IoC.Get<SessionControlService>().Release();
            IoC.Get<LauncherWindowView>()?.Close();
            IoC.Get<MainWindowView>()?.Close();
            Environment.Exit(exitCode);
        }
    }
}
