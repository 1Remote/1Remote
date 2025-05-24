using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using _1RM.Model;
using _1RM.Service;
using _1RM.View;
using _1RM.View.Guidance;
using Shawn.Utils;
using Shawn.Utils.Wpf.FileSystem;
using _1RM.Service.DataSource;
using _1RM.Utils;
using _1RM.Utils.PRemoteM;
using _1RM.Service.DataSource.DAO;
using _1RM.View.ServerList;
using _1RM.View.Settings.General;
using _1RM.View.Utils;
using System.Collections.Generic;
using _1RM.Utils.PuTTY.Model;
using _1RM.Utils.PuTTY.Model;

namespace _1RM
{
    public enum ProfileStorage
    {
        AppData,
        Portable
    }

    internal static class AppInitHelper
    {
        private static bool WritePermissionCheck(string path, bool isFile, bool alert, bool exitIfError)
        {
            Debug.Assert(LanguageServiceObj != null);
            var flag = isFile == false ? IoPermissionHelper.HasWritePermissionOnDir(path) : IoPermissionHelper.HasWritePermissionOnFile(path);
            if (flag)
            {
                // check by write txt
                var di = new DirectoryInfo(isFile ? new FileInfo(path).DirectoryName! : path);
                if (di.Exists)
                {
                    try
                    {
                        var txt = Path.Combine(di.FullName, $"PermissionCheck.txt");
                        File.WriteAllText(txt, txt);
                        File.Delete(txt);
                    }
                    catch (Exception e)
                    {
                        flag = false;
                    }
                }
            }

            if (!flag)
            {
                if (alert)
                    MessageBoxHelper.ErrorAlert(LanguageServiceObj?.Translate("write permissions alert", path) ?? "write permissions error:" + path);
                if (exitIfError)
                    Environment.Exit(1);
            }
            return flag;
        }

        private static bool CheckAllPathsPermission(AppPathHelper appPathHelper, bool alert = false, bool exitIfError = false)
        {
            var dirPaths = new List<string>
            {
                appPathHelper.BaseDirPath,
                appPathHelper.PuttyDirPath,
                appPathHelper.ProtocolRunnerDirPath,
                appPathHelper.LocalityDirPath,
                appPathHelper.LocalityIconDirPath,
            };
            var filePaths = new List<string>()
            {
                appPathHelper.LogFilePath,
                appPathHelper.ProfileJsonPath,
                appPathHelper.ProfileAdditionalDataSourceJsonPath,
                appPathHelper.SqliteDbDefaultPath,
                appPathHelper.LogFilePath,
            };

            foreach (var dirPath in dirPaths)
            {
                if (!WritePermissionCheck(dirPath, false, alert, exitIfError))
                {
                    return false;
                }
            }

            foreach (var filePath in filePaths)
            {
                if (!WritePermissionCheck(filePath, true, alert, exitIfError))
                {
                    return false;
                }
            }
            return true;
        }


        public static void Init()
        {
            SimpleLogHelper.WriteLogLevel = SimpleLogHelper.EnumLogLevel.Disabled;
            // Set salt by github action with repository secret
            UnSafeStringEncipher.Init(Assert.STRING_SALT);
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory); // in case user start app in a different working dictionary.
            SentryIoHelper.Init(Assert.SENTRY_IO_DEN);
        }

        public static LanguageService? LanguageServiceObj;
        public static KeywordMatchService? KeywordMatchServiceObj;
        public static ConfigurationService? ConfigurationServiceObj;
        public static ThemeService? ThemeServiceObj;
        public static GlobalData GlobalDataObj = null!;

        private static bool _isNewUser = false;
        private static DatabaseStatus _localDataConnectionStatus;

        public static void InitOnStart()
        {
            Debug.Assert(App.ResourceDictionary != null);

            Configuration newConfiguration = new();
            LanguageServiceObj = new LanguageService(App.ResourceDictionary!);
            LanguageServiceObj.SetLanguage(CultureInfo.CurrentCulture.Name.ToLower());
            #region Portable or not
            {
                var portablePaths = new AppPathHelper(Environment.CurrentDirectory, Environment.CurrentDirectory);
                var appDataPaths = new AppPathHelper(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Assert.APP_NAME), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Assert.APP_NAME));

                ProfileStorage selectedMode = ProfileStorage.Portable;
                {
                    _isNewUser = false;
                    bool portableProfilePathExisted = File.Exists(portablePaths.ProfileJsonPath);
                    bool appDataProfilePathExisted = File.Exists(appDataPaths.ProfileJsonPath);
                    bool forcePortable = File.Exists(AppPathHelper.FORCE_INTO_PORTABLE_MODE);
                    bool forceAppData = File.Exists(AppPathHelper.FORCE_INTO_APPDATA_MODE);
                    bool permissionForPortable = CheckAllPathsPermission(portablePaths, portableProfilePathExisted || forcePortable, forcePortable);
#if FOR_MICROSOFT_STORE_ONLY
                    forceAppData = true;
                    forcePortable = false;
                    permissionForPortable = false;
#endif


                    ProfileStorage? defaultStorage = null; // default mode, if is null, user can select `portable` or `app data` in guidance view.
                    if (permissionForPortable)
                    {
                        // 当前文件夹可写
                        if (forcePortable == true)
                        {
                            defaultStorage = selectedMode = ProfileStorage.Portable;
                            if (portableProfilePathExisted == false)
                            {
                                _isNewUser = true;
                            }
                        }
                        else if (forceAppData == true)    // 标记了强制 AppData 模式
                        {
                            defaultStorage = selectedMode = ProfileStorage.AppData;
                            if (appDataProfilePathExisted == false)
                            {
                                _isNewUser = true;
                            }
                        }
                        else // 标志文件都不存在时
                        {
                            if (portableProfilePathExisted == false) // 当前文件夹可写但却没有标志文件，也没配置文件，(不管appDataProfilePath是存在都)认为是新用户
                            {
                                _isNewUser = true;
                            }
                            else
                            {
                                defaultStorage = selectedMode = ProfileStorage.Portable;
                            }
                        }
                    }
                    else
                    {
                        // 当前文件夹不可写
                        defaultStorage = selectedMode = ProfileStorage.AppData;
                        if (appDataProfilePathExisted == false) // 当前文件夹不可写，也没 APP_DATA 配置文件，认为是新用户
                        {
                            _isNewUser = true;
                        }
                    }

                    if (_isNewUser)
                    {
                        PRemoteMTransferHelper.RunIsNeedTransferCheckAsync();
                        SecondaryVerificationHelper.Init();
                        // 新用户显示引导窗口
                        var guidanceWindowViewModel = new GuidanceWindowViewModel(LanguageServiceObj, newConfiguration, defaultStorage);
                        var guidanceWindow = new GuidanceWindow(guidanceWindowViewModel);
                        guidanceWindow.ShowDialog();
                        selectedMode = guidanceWindowViewModel.ProfileModeIsPortable ? ProfileStorage.Portable : ProfileStorage.AppData;
                        AppStartupHelper.InstallDesktopShortcut(guidanceWindowViewModel.CreateDesktopShortcut);
                    }

                    // 在当前文件夹自动创建标志文件
                    if (permissionForPortable)
                    {
                        try
                        {
                            switch (selectedMode)
                            {
                                case ProfileStorage.AppData:
                                    if (File.Exists(AppPathHelper.FORCE_INTO_APPDATA_MODE) == false)
                                        File.WriteAllText(AppPathHelper.FORCE_INTO_APPDATA_MODE, $"rename to '{AppPathHelper.FORCE_INTO_PORTABLE_MODE}' can make it portable");
                                    if (File.Exists(AppPathHelper.FORCE_INTO_PORTABLE_MODE))
                                        File.Delete(AppPathHelper.FORCE_INTO_PORTABLE_MODE);
                                    break;
                                case ProfileStorage.Portable:
                                    if (File.Exists(AppPathHelper.FORCE_INTO_PORTABLE_MODE) == false)
                                        File.WriteAllText(AppPathHelper.FORCE_INTO_PORTABLE_MODE, $"rename to '{AppPathHelper.FORCE_INTO_APPDATA_MODE}' can save to AppData");
                                    if (File.Exists(AppPathHelper.FORCE_INTO_APPDATA_MODE))
                                        File.Delete(AppPathHelper.FORCE_INTO_APPDATA_MODE);
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }
                        }
                        catch (Exception)
                        {
                            // ignored
                        }
                    }
                }
                AppPathHelper.Instance = selectedMode == ProfileStorage.Portable ? portablePaths : appDataPaths;

                // 最终文件权限检查
                {
                    var paths = AppPathHelper.Instance;
                    CheckAllPathsPermission(paths, true, true);
                }

                // 文件夹创建
                {
                    var paths = AppPathHelper.Instance;
                    AppPathHelper.CreateDirIfNotExist(paths.BaseDirPath, false);
                    AppPathHelper.CreateDirIfNotExist(paths.ProtocolRunnerDirPath, false);
                    AppPathHelper.CreateDirIfNotExist(paths.ProfileJsonPath, true);
                    AppPathHelper.CreateDirIfNotExist(paths.LogFilePath, true);
                    AppPathHelper.CreateDirIfNotExist(paths.SqliteDbDefaultPath, true);
                    AppPathHelper.CreateDirIfNotExist(paths.PuttyDirPath, false);
                    AppPathHelper.CreateDirIfNotExist(paths.LogFilePath, true);
                    AppPathHelper.CreateDirIfNotExist(paths.LocalityDirPath, false);
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

            SimpleLogHelper.WriteLogLevel = (SimpleLogHelper.EnumLogLevel)ConfigurationServiceObj.General.LogLevel;

            // make sure path is not empty
            if (string.IsNullOrWhiteSpace(ConfigurationServiceObj.LocalDataSource.Path))
            {
                ConfigurationServiceObj.LocalDataSource.Path = AppPathHelper.Instance.SqliteDbDefaultPath;
            }

            ThemeServiceObj = new ThemeService(App.ResourceDictionary!, ConfigurationServiceObj.Theme);
            GlobalDataObj = new GlobalData(ConfigurationServiceObj);
        }

        public static void InitOnConfigure()
        {
            IoC.Get<LanguageService>().SetLanguage(IoC.Get<ConfigurationService>().General.CurrentLanguageCode);

            // Init data sources controller
            var dataSourceService = IoC.Get<DataSourceService>();
            GlobalDataObj.SetDataSourceService(dataSourceService);

            // read from configs and find where db is.
            {
                var local = ConfigurationServiceObj!.LocalDataSource;
                if (string.IsNullOrWhiteSpace(local.Path))
                    local.Path = AppPathHelper.Instance.SqliteDbDefaultPath;
                var fi = new FileInfo(local.Path);
                if (fi?.Directory?.Exists == false)
                    fi.Directory.Create();
                _localDataConnectionStatus = dataSourceService.InitLocalDataSource(local);
                Task.Factory.StartNew(() =>
                {
                    //ConfigurationServiceObj!.LocalDataSource.GetServers(true);
                    IoC.Get<GlobalData>().ReloadServerList(true);
                    if (ConfigurationServiceObj.General.ShowRecentlySessionInTray)
                        IoC.Get<TaskTrayService>().ReloadTaskTrayContextMenu();
                });
            }

            // init session controller
            IoC.Get<SessionControlService>();
            IoC.Get<MainWindowViewModel>();
        }


        public static void InitOnLaunch()
        {
            if (_isNewUser == false && ConfigurationServiceObj != null)
            {
                Task.Factory.StartNew(async () =>
                {
                    try
                    {
                        var kys = new Dictionary<string, string>
                        {
                            { $"App start with - ListPageIsCardView", $"{ConfigurationServiceObj.General.ListPageIsCardView}" },
                            { $"App start with - ConfirmBeforeClosingSession", $"{ConfigurationServiceObj.General.ConfirmBeforeClosingSession}" },
                            { $"App start with - LauncherEnabled", $"{ConfigurationServiceObj.Launcher.LauncherEnabled}" },
                            { $"App start with - Theme", $"{ConfigurationServiceObj.Theme.ThemeName}" },
                            { $"App start with - Tray + ShowRecentlySessionInTray", $"{ConfigurationServiceObj.General.ShowRecentlySessionInTray}" },
                            { $"App start with - Windows Hello Enabled", $"{await SecondaryVerificationHelper.GetEnabled()}" },
                            { $"App start with - Language", $"{ConfigurationServiceObj.General.CurrentLanguageCode}" },
                        };
#if FOR_MICROSOFT_STORE_ONLY
                kys.Add("Distributor", $"{Assert.APP_NAME} MS Store");
#else
                        kys.Add("Distributor", $"{Assert.APP_NAME} Exe");
#endif

#if NETFRAMEWORK
                kys.Add($"App start with - Net", $"4.8");
#else
                        kys.Add($"App start with - Net", $"6.x");
#endif
                        SentryIoHelper.TraceSpecial(kys);
                    }
                    catch (Exception ex)
                    {
                        SentryIoHelper.Error(ex);
                    }
                });
            }

            KittyConfig.CleanUpOldConfig();
            PuttyConfig.CleanUpOldConfig();

            var mvm = IoC.Get<MainWindowViewModel>();
            if (AppStartupHelper.IsStartMinimized == false
                || _localDataConnectionStatus.Status != EnumDatabaseStatus.OK
                || _isNewUser)
            {
                if (_localDataConnectionStatus.Status != EnumDatabaseStatus.OK)
                {
                    string error = _localDataConnectionStatus.GetErrorMessage;
                    mvm.OnMainWindowViewLoaded += () =>
                    {
                        mvm.ShowMe(goPage: EnumMainWindowPage.SettingsData);
                        MessageBoxHelper.ErrorAlert(error);
                    };
                }
                else
                {
                    mvm.OnMainWindowViewLoaded += () =>
                    {
                        mvm.ShowMe(goPage: EnumMainWindowPage.List);
                        if (_isNewUser)
                        {
                            // import form PRemoteM db
                            PRemoteMTransferHelper.TransAsync();
                        }
                    };
                }
                mvm.ShowMe();
            }

            if (PRemoteMTransferHelper.IsReading == false && PRemoteMTransferHelper.AnyTransferData == false)
            {
                MaskLayerController.ShowProcessingRing("", mvm);
            }

            {
                foreach (var ds in ConfigurationServiceObj!.AdditionalDataSource)
                {
                    IoC.Get<DataSourceService>().AddOrUpdateDataSource(ds, connectTimeOutSeconds: 0, doReload: false);
                }

                if (_isNewUser == false)
                    IoC.Get<ServerListPageViewModel>().VmServerListDummyNode();

                GlobalDataObj.StartTick();
                IoC.Get<ServerListPageViewModel>().CmdRefreshDataSource.Execute();
            }

            if (PRemoteMTransferHelper.IsReading == false && PRemoteMTransferHelper.AnyTransferData == false)
            {
                MaskLayerController.HideMask(mvm);
            }

            AppStartupHelper.ProcessWhenDataLoaded(IoC.Get<GeneralSettingViewModel>());
            if (ConfigurationServiceObj.General.ShowRecentlySessionInTray)
                IoC.Get<TaskTrayService>().ReloadTaskTrayContextMenu();
            IoC.Get<LauncherWindowViewModel>().SetHotKey();
        }
    }
}
