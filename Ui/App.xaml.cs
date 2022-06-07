using System;
using System.IO;
using System.Windows;
using PRM;
using PRM.Service;
using PRM.View;
using Shawn.Utils;
using Shawn.Utils.Wpf;

namespace Ui
{
    public partial class App : Application
    {
        public static ResourceDictionary? ResourceDictionary { get; private set; } = null;

        protected override void OnStartup(StartupEventArgs e)
        {
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory); // in case user start app in a different working dictionary.
            ResourceDictionary = this.Resources;
            base.OnStartup(e);
        }

        public static void Close(int exitCode = 0)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Application.Current.Shutdown(exitCode);
            });
        }
    }
}
