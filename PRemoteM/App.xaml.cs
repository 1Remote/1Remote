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
    /*
    Defines:
        FOR_MICROSOFT_STORE_ONLY        =>  Disable all functions store not recommend.Must define FOR_MICROSOFT_STORE first!!!
    */

    public partial class App : Application
    {
        public static ResourceDictionary ResourceDictionary { get; private set; }
        public static bool IsNewUser = false;

        //public static Dispatcher UiDispatcher = null;

        private static void OnUnhandledException(Exception e)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                lock (App.Current)
                {
                    SimpleLogHelper.Fatal(e);
                    var errorReport = new ErrorReportWindow(e);
                    errorReport.ShowDialog();
#if FOR_MICROSOFT_STORE_ONLY
                    throw e;
#else
                    App.Close();
#endif 
                }
            });
        }

        private void InitExceptionHandle()
        {
            this.DispatcherUnhandledException += (o, args) =>
            {
                OnUnhandledException(args.Exception);
            };
            TaskScheduler.UnobservedTaskException += (o, args) =>
            {
                OnUnhandledException(args.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += (o, args) =>
            {
                if (args.ExceptionObject is Exception e)
                {
                    OnUnhandledException(e);
                }
                else
                {
                    SimpleLogHelper.Fatal(args.ExceptionObject);
                }
            };
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory); // in case user start app in a different working dictionary.
            ResourceDictionary = this.Resources;



            // BASE MODULES
            InitExceptionHandle();
            
            base.OnStartup(e);
        }



        public static void Close(int exitCode = 0)
        {
            IoC.Get<MainWindowView>()?.CloseMe();
            IoC.Get<LauncherWindowView>()?.Close();
            Environment.Exit(exitCode);
        }
    }
}