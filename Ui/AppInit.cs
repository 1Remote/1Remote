using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Newtonsoft.Json;
using PRM.Model;
using PRM.Model.DAO;
using PRM.Service;
using PRM.Utils;
using PRM.View;
using PRM.View.Guidance;
using PRM.View.Settings;
using PRM.View.Settings.ProtocolConfig;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.FileSystem;
using Ui;

namespace PRM
{
    internal class AppInit
    {
        private void WritePermissionCheck(string path, bool isFile)
        {
            Debug.Assert(LanguageService != null);
            var flag = isFile == false ? IoPermissionHelper.HasWritePermissionOnDir(path) : IoPermissionHelper.HasWritePermissionOnFile(path);
            if (flag == false)
            {
                MessageBox.Show(LanguageService.Translate("write permissions alert", path), LanguageService.Translate("messagebox_title_warning"), MessageBoxButton.OK);
                Environment.Exit(1);
            }
        }

        private static void CreateDirIfNotExist(string path, bool isFile)
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
                di.Create();
            }
        }



        public LanguageService? LanguageService;
        public KeywordMatchService? KeywordMatchService;
        public ConfigurationService? ConfigurationService;
        public ThemeService? ThemeService;
        public GlobalData GlobalData = null!;
        public Configuration Configuration = new Configuration();

        public void InitOnStart()
        {
            Debug.Assert(App.ResourceDictionary != null);
            App.OnlyOneAppInstanceCheck();

            LanguageService = new LanguageService(App.ResourceDictionary);
            LanguageService.SetLanguage(CultureInfo.CurrentCulture.Name.ToLower());
            #region Portable mode or not
            {
                var portablePaths = new AppPathHelper(Environment.CurrentDirectory);
                var appDataPaths = new AppPathHelper(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppPathHelper.APP_NAME));
                // 读取旧版本配置信息 TODO remove after 2023.01.01
                {
                    string iniPath = portablePaths.IniProfilePath;
                    string dbDefaultPath = portablePaths.DefaultSqliteDbPath;
                    if (File.Exists(iniPath) == false)
                    {
                        iniPath = appDataPaths.IniProfilePath;
                        dbDefaultPath = appDataPaths.DefaultSqliteDbPath;
                    }
                    if (File.Exists(iniPath) == true)
                    {
                        try
                        {
                            Configuration = ConfigurationService.LoadFromIni(iniPath, dbDefaultPath);
                        }
                        finally
                        {
                            File.Delete(iniPath);
                        }
                    }
                }

                bool isPortableMode = false;
                bool portableProfilePathExisted = File.Exists(portablePaths.JsonProfilePath);
                bool appDataProfilePathExisted = File.Exists(appDataPaths.JsonProfilePath);
                if (portableProfilePathExisted == false && appDataProfilePathExisted == false)
                {
                    // 新用户显示引导窗口
                    _isNewUser = true;
                    var guidanceWindowViewModel = new GuidanceWindowViewModel(LanguageService, Configuration);
                    var guidanceWindow = new GuidanceWindow(guidanceWindowViewModel);
                    guidanceWindow.ShowDialog();
                    isPortableMode = guidanceWindowViewModel.ProFileMode == EProFileMode.Portable;
                    AppPathHelper.Instance = isPortableMode ? portablePaths : appDataPaths;
                }
                else if (portableProfilePathExisted)
                {
                    isPortableMode = true;
                }
                else if (appDataProfilePathExisted)
                {
                    isPortableMode = false;
                }
                AppPathHelper.Instance = isPortableMode ? portablePaths : appDataPaths;

                // 最终文件权限检查
                {
                    var paths = AppPathHelper.Instance;
                    WritePermissionCheck(paths.BaseDirPath, false);
                    WritePermissionCheck(paths.ProtocolRunnerDirPath, false);
                    WritePermissionCheck(paths.JsonProfilePath, true);
                    WritePermissionCheck(paths.LogFilePath, true);
                    WritePermissionCheck(paths.DefaultSqliteDbPath, true);
                    WritePermissionCheck(paths.KittyDirPath, false);
                    WritePermissionCheck(paths.LocalityDirPath, false);
                }

                // 文件夹创建
                {
                    var paths = AppPathHelper.Instance;
                    CreateDirIfNotExist(paths.BaseDirPath, false);
                    CreateDirIfNotExist(paths.ProtocolRunnerDirPath, false);
                    CreateDirIfNotExist(paths.JsonProfilePath, true);
                    CreateDirIfNotExist(paths.LogFilePath, true);
                    CreateDirIfNotExist(paths.DefaultSqliteDbPath, true);
                    CreateDirIfNotExist(paths.KittyDirPath, false);
                    CreateDirIfNotExist(paths.LocalityDirPath, false);
                }
            }
            #endregion

            // logger init
            {
#if DEBUG
                SimpleLogHelper.WriteLogLevel = SimpleLogHelper.EnumLogLevel.Debug;
                ConsoleManager.Show();
#endif
                SimpleLogHelper.WriteLogLevel = SimpleLogHelper.EnumLogLevel.Warning;
                SimpleLogHelper.PrintLogLevel = SimpleLogHelper.EnumLogLevel.Debug;
                // init log file placement
                var fi = new FileInfo(AppPathHelper.Instance.LogFilePath);
                if (fi?.Directory?.Exists == false)
                    fi.Directory.Create();
                SimpleLogHelper.LogFileName = AppPathHelper.Instance.LogFilePath;
            }

            // 读取配置
            if (File.Exists(AppPathHelper.Instance.JsonProfilePath))
            {
                try
                {
                    var tmp = JsonConvert.DeserializeObject<Configuration>(File.ReadAllText(AppPathHelper.Instance.JsonProfilePath));
                    if (tmp != null)
                        Configuration = tmp;
                }
                catch
                {
                    // ignored
                }
            }
            else
            {
                Configuration.Database.SqliteDatabasePath = AppPathHelper.Instance.DefaultSqliteDbPath;
            }

            KeywordMatchService = new KeywordMatchService();
            ConfigurationService = new ConfigurationService(Configuration, KeywordMatchService);
            ThemeService = new ThemeService(App.ResourceDictionary, ConfigurationService.Theme);
            GlobalData = new GlobalData(ConfigurationService);
        }

        private bool _isNewUser = false;
        private EnumDbStatus _dbConnectionStatus;

        public void InitOnConfigure()
        {
            IoC.Get<LanguageService>().SetLanguage(IoC.Get<ConfigurationService>().General.CurrentLanguageCode);
            var context = IoC.Get<PrmContext>();
            _dbConnectionStatus = context.InitSqliteDb();
            IoC.Get<GlobalData>().ReloadServerList();
            IoC.Get<SessionControlService>();
        }

        public void InitOnLaunch()
        {

            if (_dbConnectionStatus != EnumDbStatus.OK)
            {
                string error = _dbConnectionStatus.GetErrorInfo();
                MessageBox.Show(error, IoC.Get<LanguageService>().Translate("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                IoC.Get<MainWindowViewModel>().CmdGoSysOptionsPage.Execute("Data");
                IoC.Get<MainWindowViewModel>().ActivateMe();
            }


            if (IoC.Get<ConfigurationService>().General.AppStartMinimized == false
                || _isNewUser)
            {
                IoC.Get<MainWindowViewModel>().ActivateMe();
            }

            // After startup and initalizing our application and when closing our window and minimize the application to tray we free memory with the following line:
            System.Diagnostics.Process.GetCurrentProcess().MinWorkingSet = System.Diagnostics.Process.GetCurrentProcess().MinWorkingSet;
        }
    }
}
