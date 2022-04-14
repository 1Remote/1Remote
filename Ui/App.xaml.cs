using System;
using System.IO;
using System.Windows;
using PRM.View;

namespace Ui
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static ResourceDictionary ResourceDictionary { get; private set; } = new ResourceDictionary();

        protected override void OnStartup(StartupEventArgs e)
        {
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory); // in case user start app in a different working dictionary.
            ResourceDictionary = this.Resources;
            base.OnStartup(e);
        }

        public static void Close(int exitCode = 0)
        {
            IoC.Get<LauncherWindowView>()?.Close();
            IoC.Get<MainWindowView>()?.Close();
            Environment.Exit(exitCode);
        }
    }
}
