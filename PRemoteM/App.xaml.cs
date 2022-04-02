using System;
using System.IO;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Shawn.Utils;
using PRM.Model;
using PRM.Model.DAO;
using PRM.Model.DAO.Dapper;
using PRM.Service;
using PRM.Utils.KiTTY;
using PRM.View;
using PRM.View.ErrorReport;
using PRM.View.Guidance;
using PRM.View.Settings;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.FileSystem;

namespace PRM
{
    public partial class App : Application
    {
        public static ResourceDictionary ResourceDictionary { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory); // in case user start app in a different working dictionary.
            ResourceDictionary = this.Resources;
            base.OnStartup(e);
        }

        public static void Close(int exitCode = 0)
        {
            IoC.Get<LauncherWindowView>()?.Close();
            IoC.Get<MainWindowView>()?.CloseMe();
            Environment.Exit(exitCode);
        }
    }
}