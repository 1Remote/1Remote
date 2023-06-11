using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using _1RM.Model;
using _1RM.Service;
using _1RM.View;
using _1RM.View.Guidance;
using Shawn.Utils;
using Shawn.Utils.Wpf.FileSystem;
using _1RM.Service.DataSource;
using _1RM.Utils;
using _1RM.Utils.KiTTY.Model;
using _1RM.Utils.PRemoteM;
using _1RM.Service.DataSource.DAO;
using _1RM.Service.Locality;
using _1RM.View.Utils;

namespace _1RM
{
    internal class AppInit
    {
        private void WritePermissionCheck(string path, bool isFile)
        {
            Debug.Assert(LanguageServiceObj != null);
            var flag = isFile == false ? IoPermissionHelper.HasWritePermissionOnDir(path) : IoPermissionHelper.HasWritePermissionOnFile(path);
            if (flag == false)
            {
                MessageBoxHelper.ErrorAlert(LanguageServiceObj?.Translate("write permissions alert", path) ?? "write permissions error:" + path);
                Environment.Exit(1);
            }
        }


        public static void InitOnStartup(string[] args)
        {
            SimpleLogHelper.WriteLogLevel = SimpleLogHelper.EnumLogLevel.Disabled;
            AppStartupHelper.Init(args); // in this method, it will call App.Close() if needed
            // Set salt by github action with repository secret
            UnSafeStringEncipher.Init(Assert.STRING_SALT);
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory); // in case user start app in a different working dictionary.

            MsAppCenterHelper.Init(Assert.MS_APP_CENTER_SECRET);
        }

        public LanguageService? LanguageServiceObj;
        public KeywordMatchService? KeywordMatchServiceObj;
        public ConfigurationService? ConfigurationServiceObj;
        public ThemeService? ThemeServiceObj;
        public GlobalData GlobalDataObj = null!;

        public void InitOnStart()
        {
            Debug.Assert(App.ResourceDictionary != null);

            Configuration newConfiguration = new();
            LanguageServiceObj = new LanguageService(App.ResourceDictionary!);
            LanguageServiceObj.SetLanguage(CultureInfo.CurrentCulture.Name.ToLower());
            #region Portable or not
            {
                var portablePaths = new AppPathHelper(Environment.CurrentDirectory);
                var appDataPaths = new AppPathHelper(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Assert.APP_NAME));

                bool isPortableMode = false;
                {
                    _isNewUser = false;
                    bool portableProfilePathExisted = File.Exists(portablePaths.ProfileJsonPath);
                    bool appDataProfilePathExisted = File.Exists(appDataPaths.ProfileJsonPath);
                    bool forcePortable = File.Exists(AppPathHelper.FORCE_INTO_PORTABLE_MODE);
                    bool forceAppData = File.Exists(AppPathHelper.FORCE_INTO_APPDATA_MODE);
                    bool permissionForPortable = AppPathHelper.CheckPermissionForPortablePaths();
#if FOR_MICROSOFT_STORE_ONLY
                    forceAppData = true;
                    forcePortable = false;
                    permissionForPortable = false;
#endif
                    bool profileModeIsPortable = false;
                    bool profileModeIsEnabled = true;

                    if (forcePortable == true && forceAppData == false)
                    {
                        isPortableMode = true;
                        if (portableProfilePathExisted == false)
                        {
                            profileModeIsPortable = true;
                            profileModeIsEnabled = false;
                            _isNewUser = true;
                        }
                    }
                    else if (forcePortable == false && forceAppData == true)    // 标记了强制 AppData 模式
                    {
                        isPortableMode = false;
                        if (appDataProfilePathExisted == false)
                        {
                            profileModeIsPortable = false;
                            profileModeIsEnabled = false;
                            _isNewUser = true;
                        }
                    }
                    else // 标志文件都存在或都不存在时
                    {
                        if (File.Exists(AppPathHelper.FORCE_INTO_APPDATA_MODE))
                            File.Delete(AppPathHelper.FORCE_INTO_APPDATA_MODE);
                        if (File.Exists(AppPathHelper.FORCE_INTO_PORTABLE_MODE))
                            File.Delete(AppPathHelper.FORCE_INTO_PORTABLE_MODE);


                        if (portableProfilePathExisted)
                        {
                            isPortableMode = true;
                        }
                        else if (permissionForPortable == false)
                        {
                            isPortableMode = false;
                            if (appDataProfilePathExisted == false)
                            {
                                profileModeIsPortable = false;
                                profileModeIsEnabled = false;
                                _isNewUser = true;
                            }
                        }
                        else
                        {
                            // portable 配置文件不存在，无论 app_data 的配置是否存在都进引导
                            profileModeIsPortable = !appDataProfilePathExisted;
                            profileModeIsEnabled = true;
                            _isNewUser = true;
                        }
                    }

                    if (_isNewUser)
                    {
                        PRemoteMTransferHelper.RunIsNeedTransferCheckAsync();
                        // 新用户显示引导窗口
                        var guidanceWindowViewModel = new GuidanceWindowViewModel(LanguageServiceObj, newConfiguration, profileModeIsPortable, profileModeIsEnabled);
                        var guidanceWindow = new GuidanceWindow(guidanceWindowViewModel);
                        guidanceWindow.ShowDialog();
                        isPortableMode = guidanceWindowViewModel.ProfileModeIsPortable;
                        if (guidanceWindowViewModel.AppStartAutomatically)
                        {
                            ConfigurationService.SetSelfStart(true);
                        }
                    }

                    // 自动创建标志文件
                    if (permissionForPortable)
                    {
                        try
                        {
                            if (isPortableMode)
                            {
                                if (File.Exists(AppPathHelper.FORCE_INTO_PORTABLE_MODE) == false)
                                    File.WriteAllText(AppPathHelper.FORCE_INTO_PORTABLE_MODE, $"rename to '{AppPathHelper.FORCE_INTO_APPDATA_MODE}' can save to AppData");
                                if (File.Exists(AppPathHelper.FORCE_INTO_APPDATA_MODE))
                                    File.Delete(AppPathHelper.FORCE_INTO_APPDATA_MODE);
                            }
                            if (isPortableMode == false)
                            {
                                if (File.Exists(AppPathHelper.FORCE_INTO_APPDATA_MODE) == false)
                                    File.WriteAllText(AppPathHelper.FORCE_INTO_APPDATA_MODE, $"rename to '{AppPathHelper.FORCE_INTO_PORTABLE_MODE}' can make it portable");
                                if (File.Exists(AppPathHelper.FORCE_INTO_PORTABLE_MODE))
                                    File.Delete(AppPathHelper.FORCE_INTO_PORTABLE_MODE);
                            }
                        }
                        catch (Exception)
                        {
                            // ignored
                        }
                    }
                }
                AppPathHelper.Instance = isPortableMode ? portablePaths : appDataPaths;

                // 最终文件权限检查
                {
                    var paths = AppPathHelper.Instance;
                    WritePermissionCheck(paths.BaseDirPath, false);
                    WritePermissionCheck(paths.ProtocolRunnerDirPath, false);
                    WritePermissionCheck(paths.ProfileJsonPath, true);
                    WritePermissionCheck(paths.LogFilePath, true);
                    //WritePermissionCheck(paths.SqliteDbDefaultPath, true);
                    WritePermissionCheck(paths.KittyDirPath, false);
                    WritePermissionCheck(paths.LocalityDirPath, false);
                }

                // 文件夹创建
                {
                    var paths = AppPathHelper.Instance;
                    AppPathHelper.CreateDirIfNotExist(paths.BaseDirPath, false);
                    AppPathHelper.CreateDirIfNotExist(paths.ProtocolRunnerDirPath, false);
                    AppPathHelper.CreateDirIfNotExist(paths.ProfileJsonPath, true);
                    AppPathHelper.CreateDirIfNotExist(paths.LogFilePath, true);
                    AppPathHelper.CreateDirIfNotExist(paths.SqliteDbDefaultPath, true);
                    AppPathHelper.CreateDirIfNotExist(paths.KittyDirPath, false);
                    AppPathHelper.CreateDirIfNotExist(paths.LogFilePath, true);
                    AppPathHelper.CreateDirIfNotExist(paths.LocalityDirPath, false);
                    if (File.Exists(paths.LocalityJsonPath))
                    {
                        if (File.Exists(LocalityService.JsonPath) == false)
                            File.Move(paths.LocalityJsonPath, LocalityService.JsonPath);
                    }
                    if (File.Exists(paths.LocalityConnectTimeRecord))
                    {
                        if (File.Exists(Path.Combine(paths.LocalityDirPath, "ConnectionRecords.json")) == false)
                            File.Move(paths.LocalityConnectTimeRecord, Path.Combine(paths.LocalityDirPath, "ConnectionRecords.json"));
                    }
                }
            }
            #endregion

            // logger init
            {
                SimpleLogHelper.WriteLogLevel = SimpleLogHelper.EnumLogLevel.Info;
                SimpleLogHelper.PrintLogLevel = SimpleLogHelper.EnumLogLevel.Debug;
#if DEBUG
                SimpleLogHelper.WriteLogLevel = SimpleLogHelper.EnumLogLevel.Debug;
                ConsoleManager.Show();
#endif
                // init log file placement
                SimpleLogHelper.LogFileName = AppPathHelper.Instance.LogFilePath;
            }


            KeywordMatchServiceObj = new KeywordMatchService();
            // read profile
            try
            {
                if (File.Exists(AppPathHelper.Instance.ProfileJsonPath) == true)
                {
                    ConfigurationServiceObj = ConfigurationService.LoadFromAppPath(KeywordMatchServiceObj);
                }
                else
                {
                    newConfiguration.SqliteDatabasePath = AppPathHelper.Instance.SqliteDbDefaultPath;
                    ConfigurationServiceObj = new ConfigurationService(KeywordMatchServiceObj, newConfiguration);
                }
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
                newConfiguration.SqliteDatabasePath = AppPathHelper.Instance.SqliteDbDefaultPath;
                ConfigurationServiceObj = new ConfigurationService(KeywordMatchServiceObj, newConfiguration);
            }

            // make sure path is not empty
            if (string.IsNullOrWhiteSpace(ConfigurationServiceObj.LocalDataSource.Path))
            {
                ConfigurationServiceObj.LocalDataSource.Path = AppPathHelper.Instance.SqliteDbDefaultPath;
            }

            ThemeServiceObj = new ThemeService(App.ResourceDictionary!, ConfigurationServiceObj.Theme);
            GlobalDataObj = new GlobalData(ConfigurationServiceObj);
        }

        private bool _isNewUser = false;
        private EnumDatabaseStatus _localDataConnectionStatus;

        public void InitOnConfigure()
        {
            IoC.Get<LanguageService>().SetLanguage(IoC.Get<ConfigurationService>().General.CurrentLanguageCode);

            // Init data sources controller
            var dataSourceService = IoC.Get<DataSourceService>();
            GlobalDataObj.SetDataSourceService(dataSourceService);
            GlobalDataObj.ReloadServerList(true);

            // read from configs and find where db is.
            {
                var sqliteConfig = ConfigurationServiceObj!.LocalDataSource;
                if (string.IsNullOrWhiteSpace(sqliteConfig.Path))
                    sqliteConfig.Path = AppPathHelper.Instance.SqliteDbDefaultPath;
                var fi = new FileInfo(sqliteConfig.Path);
                if (fi?.Directory?.Exists == false)
                    fi.Directory.Create();
                _localDataConnectionStatus = dataSourceService.InitLocalDataSource(sqliteConfig);
            }

            // init session controller
            IoC.Get<SessionControlService>();
            IoC.Get<MainWindowViewModel>();

            if (_isNewUser)
            {
                ConfigurationServiceObj.SetSelfStart();
            }
        }


        public void InitOnLaunch()
        {
            if (_isNewUser == false && ConfigurationServiceObj != null)
            {
                MsAppCenterHelper.TraceSpecial($"App start with - ListPageIsCardView", $"{ConfigurationServiceObj.General.ListPageIsCardView}");
                MsAppCenterHelper.TraceSpecial($"App start with - ConfirmBeforeClosingSession", $"{ConfigurationServiceObj.General.ConfirmBeforeClosingSession}");
                MsAppCenterHelper.TraceSpecial($"App start with - LauncherEnabled", $"{ConfigurationServiceObj.Launcher.LauncherEnabled}");
                MsAppCenterHelper.TraceSpecial($"App start with - Theme", $"{ConfigurationServiceObj.Theme.ThemeName}");
#if NETFRAMEWORK
                MsAppCenterHelper.TraceSpecial($"App start with - Net", $"4.8");
#else
                MsAppCenterHelper.TraceSpecial($"App start with - Net", $"6.x");
#endif
            }

            KittyConfig.CleanUpOldConfig();


            bool appStartMinimized = AppStartupHelper.IsStartMinimized;
            if (appStartMinimized == false
                || _localDataConnectionStatus != EnumDatabaseStatus.OK
                || _isNewUser)
            {
                if (_localDataConnectionStatus != EnumDatabaseStatus.OK)
                {
                    string error = _localDataConnectionStatus.GetErrorInfo();
                    IoC.Get<MainWindowViewModel>().OnMainWindowViewLoaded += () =>
                    {
                        IoC.Get<MainWindowViewModel>().ShowMe(goPage: EnumMainWindowPage.SettingsData);
                        MessageBoxHelper.ErrorAlert(error);
                    };
                }
                else
                {
                    IoC.Get<MainWindowViewModel>().OnMainWindowViewLoaded += () =>
                    {
                        IoC.Get<MainWindowViewModel>().ShowMe(goPage: EnumMainWindowPage.List);
                        if (_isNewUser)
                        {
                            // import form PRemoteM db
                            PRemoteMTransferHelper.TransAsync();
                        }
                        else
                        {
                            MaskLayerController.ShowProcessingRing();
                        }
                    };
                }
                IoC.Get<MainWindowViewModel>().ShowMe();
            }


            Task.Factory.StartNew(() =>
            {
                //// read from primary database
                //IoC.Get<GlobalData>().ReloadServerList(true);
                // read from AdditionalDataSource async
                if (ConfigurationServiceObj!.AdditionalDataSource.Any())
                    Task.WaitAll(ConfigurationServiceObj!.AdditionalDataSource.Select(config =>
                        Task.Factory.StartNew(() =>
                        {
                            IoC.Get<DataSourceService>().AddOrUpdateDataSource(config, doReload: false);
                        })).ToArray());
                IoC.Get<GlobalData>().ReloadServerList(true);
                MaskLayerController.HideMask();
            });
        }
    }
}
