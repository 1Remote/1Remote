﻿using System;
using System.IO;
using System.Windows;
using _1RM.Utils;

namespace _1RM
{
    public partial class App : Application
    {
        public static ResourceDictionary? ResourceDictionary { get; private set; } = null;

        protected override void OnStartup(StartupEventArgs e)
        {
            MsAppCenterHelper.Init(Assert.MS_APP_CENTER_SECRET);

            AppInit.InitOnStartup();
            ResourceDictionary = this.Resources;
            base.OnStartup(e);
        }

        public static bool ExitingFlag = false;
        public static void Close(int exitCode = 0)
        {
            ExitingFlag = true;
            Application.Current.Dispatcher.Invoke(() =>
            {
                Application.Current.Shutdown(exitCode);
            });
        }
    }
}
