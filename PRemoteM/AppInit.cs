using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using PRM.Service;
using PRM.View;
using Shawn.Utils;
using Shawn.Utils.Wpf;

namespace PRM
{
    internal class AppInit
    {
        public static void InitLog(bool canPortable)
        {
#if DEV
            SimpleLogHelper.WriteLogLevel = SimpleLogHelper.EnumLogLevel.Debug;
            ConsoleManager.Show();
#endif

            var baseDir = canPortable ? Environment.CurrentDirectory : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ConfigurationService.AppName);

            SimpleLogHelper.WriteLogLevel = SimpleLogHelper.EnumLogLevel.Warning;
            SimpleLogHelper.PrintLogLevel = SimpleLogHelper.EnumLogLevel.Debug;
            // init log file placement
            var logFilePath = Path.Combine(baseDir, "Logs", $"{ConfigurationService.AppName}.log.md");
            var fi = new FileInfo(logFilePath);
            if (!fi.Directory.Exists)
                fi.Directory.Create();
            SimpleLogHelper.LogFileName = logFilePath;

            // old version log files cleanup
            if (canPortable)
            {
                var diLogs = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ConfigurationService.AppName, "Logs"));
                if (diLogs.Exists)
                    diLogs.Delete(true);
                var diApp = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ConfigurationService.AppName));
                if (diApp.Exists)
                {
                    var fis = diApp.GetFiles("*.md");
                    foreach (var info in fis)
                    {
                        info.Delete();
                    }
                }
            }
        }




        private static NamedPipeHelper _namedPipeHelper;
        public static void OnlyOneAppInstanceCheck()
        {
#if FOR_MICROSOFT_STORE_ONLY
            string instanceName = ConfigurationService.AppName + "_Store_" + MD5Helper.GetMd5Hash16BitString(Environment.UserName);
#else
            string instanceName = ConfigurationService.AppName + "_" + MD5Helper.GetMd5Hash16BitString(Environment.CurrentDirectory + Environment.UserName);
#endif
            _namedPipeHelper = new NamedPipeHelper(instanceName);
            if (_namedPipeHelper.IsServer == false)
            {
                try
                {
                    _namedPipeHelper.NamedPipeSendMessage("ActivateMe");
                    Environment.Exit(0);
                }
                catch (Exception e)
                {
                    Environment.Exit(1);
                }
            }

            _namedPipeHelper.OnMessageReceived += message =>
            {
                SimpleLogHelper.Debug("NamedPipeServerStream get: " + message);
                if (message == "ActivateMe")
                {
                    IoC.Get<MainWindowView>()?.ActivateMe();
                }
            };
        }
    }
}
