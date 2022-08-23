using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using _1RM.View.Guidance;
using Shawn.Utils.Wpf.FileSystem;

namespace _1RM.Service
{
    public class AppPathHelper
    {
        private const string AppName = "1Remote";
#if DEBUG
        public const string APP_NAME = $"{AppName}_Debug";
#if FOR_MICROSOFT_STORE_ONLY
        public const string APP_DISPLAY_NAME = $"{APP_NAME}(Store)_Debug";
#else
        public const string APP_DISPLAY_NAME = APP_NAME;
#endif
#else
        public const string APP_NAME = $"{AppName}";
#if FOR_MICROSOFT_STORE_ONLY
        public const string APP_DISPLAY_NAME = $"{APP_NAME}(Store)";
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
            var flag = isFile == false ? IoPermissionHelper.HasWritePermissionOnDir(path) : IoPermissionHelper.HasWritePermissionOnFile(path);
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
            && WritePermissionCheck(paths.ProfileJsonPath, true)
            && WritePermissionCheck(paths.LogFilePath, true)
            && WritePermissionCheck(paths.SqliteDbDefaultPath, true)
            && WritePermissionCheck(paths.KittyDirPath, false)
            && WritePermissionCheck(paths.LocalityJsonPath, true))
            {
                return true;
            }
            return false;
        }

        public const string FORCE_INTO_PORTABLE_MODE = "FORCE_INTO_PORTABLE_MODE";
        public const string FORCE_INTO_APPDATA_MODE = "FORCE_INTO_APPDATA_MODE";

        public string LogFilePath => Path.Combine(BaseDirPath, "Logs", $"{APP_NAME}.log.md");
        public string ProfileJsonPath => Path.Combine(BaseDirPath, APP_NAME + ".json");
        public string SqliteDbDefaultPath => Path.Combine(BaseDirPath, $"{APP_NAME}.db");
        public string ProtocolRunnerDirPath => Path.Combine(BaseDirPath, "Protocols");
        public string KittyDirPath => Path.Combine(BaseDirPath, "Kitty");
        public string LocalityJsonPath => Path.Combine(BaseDirPath, "Locality.json");
        public string ConnectTimeRecord => Path.Combine(BaseDirPath, "ConnectTimeRecord.json");



        public static AppPathHelper Instance { get; set; } = null!;
    }
}
