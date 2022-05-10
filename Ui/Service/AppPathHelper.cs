using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using PRM.View.Guidance;
using Shawn.Utils.Wpf.FileSystem;

namespace PRM.Service
{
    public class AppPathHelper
    {
        private const string AppName = "PRemoteM";
#if DEBUG
        public const string APP_NAME = $"{AppName}_Debug";
#if FOR_MICROSOFT_STORE_ONLY
        public const string APP_DISPLAY_NAME = $"{_appName}(Store)_Debug";
#else
        public const string APP_DISPLAY_NAME = APP_NAME;
#endif
#else
        public const string AppName = $"{_appName}";
#if FOR_MICROSOFT_STORE_ONLY
        public const string APP_DISPLAY_NAME = $"{_appName}(Store)";
#else
        public const string APP_DISPLAY_NAME = APP_NAME;
#endif
#endif

        public readonly string BaseDirPath;

        public AppPathHelper(string baseDirPath)
        {
            BaseDirPath = baseDirPath;
        }

        private static bool WritePermissionCheck(string path, bool isFile)
        {
            bool flag = false;
            flag = isFile == false ? IoPermissionHelper.HasWritePermissionOnDir(path) : IoPermissionHelper.HasWritePermissionOnFile(path);
            return flag;
        }


        public static bool CheckPermissionForPortablePaths()
        {
#if FOR_MICROSOFT_STORE_ONLY
            return false;
#endif
            var paths = new AppPathHelper(Environment.CurrentDirectory);

            if(WritePermissionCheck(paths.BaseDirPath, false)
            && WritePermissionCheck(paths.ProtocolRunnerDirPath, false)
            && WritePermissionCheck(paths.JsonProfilePath, true)
            && WritePermissionCheck(paths.LogFilePath, true)
            && WritePermissionCheck(paths.DefaultSqliteDbPath, true)
            && WritePermissionCheck(paths.KittyDirPath, false)
            && WritePermissionCheck(paths.LocalityDirPath, false))
            {
                return true;
            }
            return false;
        }

        public string LogFilePath => Path.Combine(BaseDirPath, "Logs", $"{APP_NAME}.log.md");
        public string JsonProfilePath => Path.Combine(BaseDirPath, APP_NAME + ".json");
        [Obsolete]
        public string IniProfilePath => Path.Combine(BaseDirPath, APP_NAME + ".ini");
        public string DefaultSqliteDbPath => Path.Combine(BaseDirPath, $"{APP_NAME}.db");
        public string ProtocolRunnerDirPath => Path.Combine(BaseDirPath, "Protocols");
        public string KittyDirPath => Path.Combine(BaseDirPath, "Kitty");
        public string LocalityDirPath => Path.Combine(BaseDirPath, "Locality");



        public static AppPathHelper Instance { get; set; } = null!;
    }
}
