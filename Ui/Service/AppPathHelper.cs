using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using _1RM.Utils;
using _1RM.View.Guidance;
using Shawn.Utils;
using Shawn.Utils.Wpf.FileSystem;

namespace _1RM.Service
{
    public class AppPathHelper
    {
        public static AppPathHelper Instance { get; set; } = new AppPathHelper(Environment.CurrentDirectory, Environment.CurrentDirectory);

        public readonly string BaseDirPath;
        public readonly string BaseDirPathForLocality;

        public static void CreateDirIfNotExist(string path, bool isFile)
        {
            DirectoryInfo? di = null;
            if (isFile)
            {
                var fi = new FileInfo(path);
                if (fi.Directory?.Exists == false)
                {
                    di = fi.Directory;
                }
            }
            else
            {
                di = new DirectoryInfo(path);
            }
            if (di?.Exists == false)
            {
                try
                {
                    di.Create();
                }
                catch (Exception e)
                {
                    SimpleLogHelper.Error(e);
                }
            }
        }

        public AppPathHelper(string baseDirPath, string baseDirPathForLocality)
        {
            BaseDirPath = baseDirPath;
            BaseDirPathForLocality = baseDirPathForLocality;
        }

        public const string FORCE_INTO_PORTABLE_MODE = "FORCE_INTO_PORTABLE_MODE";
        public const string FORCE_INTO_APPDATA_MODE = "FORCE_INTO_APPDATA_MODE";

        #region Remoting
        public string ProfileJsonPath => Path.Combine(BaseDirPath, Assert.APP_NAME + ".json");
        public string ProfileAdditionalDataSourceJsonPath => Path.Combine(BaseDirPath, Assert.APP_NAME + ".dataSources.json");
        public string SqliteDbDefaultPath => Path.Combine(BaseDirPath, $"{Assert.APP_NAME}.db");
        public string ProtocolRunnerDirPath => Path.Combine(BaseDirPath, "Protocols");
        #endregion


        #region Locality
        public string LogFilePath => Path.Combine(BaseDirPathForLocality, ".logs", $"{Assert.APP_NAME}.log.md");
        public string LocalityDirPath => Path.Combine(BaseDirPathForLocality, ".locality");
        public string LocalityIconDirPath => Path.Combine(BaseDirPathForLocality, ".icons");
        public string KittyDirPath => Path.Combine(BaseDirPathForLocality, "KiTTY");
        public string PuttyDirPath => Path.Combine(BaseDirPathForLocality, "PuTTY");
        #endregion
    }
}
