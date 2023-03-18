﻿using System;
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
using _1RM.Model;
using _1RM.Model.DAO;
using _1RM.Service;
using _1RM.View;
using _1RM.View.Guidance;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.FileSystem;
using Stylet;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.Utils;
using _1RM.Utils.KiTTY;
using _1RM.Utils.KiTTY.Model;
using _1RM.Utils.PRemoteM;

namespace _1RM
{
    internal class AppInit
    {
        private void WritePermissionCheck(string path, bool isFile)
        {
            Debug.Assert(LanguageService != null);
            var flag = isFile == false ? IoPermissionHelper.HasWritePermissionOnDir(path) : IoPermissionHelper.HasWritePermissionOnFile(path);
            if (flag == false)
            {
                MessageBox.Show(LanguageService.Translate("write permissions alert", path), LanguageService.Translate("Warning"), MessageBoxButton.OK);
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

        public static void InitOnStartup()
        {
            SimpleLogHelper.WriteLogLevel = SimpleLogHelper.EnumLogLevel.Disabled;
            // TODO Set salt by github action with repository secret
            UnSafeStringEncipher.Init("***SALT***");
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory); // in case user start app in a different working dictionary.
        }

        public LanguageService? LanguageService;
        public KeywordMatchService? KeywordMatchService;
        public ConfigurationService? ConfigurationService;
        public ThemeService? ThemeService;
        public GlobalData GlobalData = null!;
        public Configuration NewConfiguration = new();

        public void InitOnStart()
        {
            Debug.Assert(App.ResourceDictionary != null);

            LanguageService = new LanguageService(App.ResourceDictionary);
            LanguageService.SetLanguage(CultureInfo.CurrentCulture.Name.ToLower());
            #region Portable mode or not
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
                        if (PRemoteMTransferHelper.IsNeedTransfer())
                        {
                            PRemoteMTransferHelper.ReadAsync();
                        }

                        // 新用户显示引导窗口
                        var guidanceWindowViewModel = new GuidanceWindowViewModel(LanguageService, NewConfiguration, profileModeIsPortable, profileModeIsEnabled);
                        var guidanceWindow = new GuidanceWindow(guidanceWindowViewModel);
                        guidanceWindow.ShowDialog();
                        isPortableMode = guidanceWindowViewModel.ProfileModeIsPortable;
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
                    WritePermissionCheck(paths.LocalityJsonPath, true);
                }

                // 文件夹创建
                {
                    var paths = AppPathHelper.Instance;
                    CreateDirIfNotExist(paths.BaseDirPath, false);
                    CreateDirIfNotExist(paths.ProtocolRunnerDirPath, false);
                    CreateDirIfNotExist(paths.ProfileJsonPath, true);
                    CreateDirIfNotExist(paths.LogFilePath, true);
                    CreateDirIfNotExist(paths.SqliteDbDefaultPath, true);
                    CreateDirIfNotExist(paths.KittyDirPath, false);
                    CreateDirIfNotExist(paths.LocalityJsonPath, true);
                }
            }
            #endregion

            // logger init
            {
#if DEBUG
                SimpleLogHelper.WriteLogLevel = SimpleLogHelper.EnumLogLevel.Debug;
                ConsoleManager.Show();
#endif
                SimpleLogHelper.WriteLogLevel = SimpleLogHelper.EnumLogLevel.Info;
                SimpleLogHelper.PrintLogLevel = SimpleLogHelper.EnumLogLevel.Debug;
                // init log file placement
                var fi = new FileInfo(AppPathHelper.Instance.LogFilePath);
                if (fi?.Directory?.Exists == false)
                    fi.Directory.Create();
                SimpleLogHelper.LogFileName = AppPathHelper.Instance.LogFilePath;
            }


            KeywordMatchService = new KeywordMatchService();
            // read profile
            try
            {
                if (File.Exists(AppPathHelper.Instance.ProfileJsonPath) == true)
                {
                    ConfigurationService = ConfigurationService.LoadFromAppPath(KeywordMatchService);
                }
                else
                {
                    NewConfiguration.SqliteDatabasePath = AppPathHelper.Instance.SqliteDbDefaultPath;
                    ConfigurationService = new ConfigurationService(KeywordMatchService, NewConfiguration);
                }
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
                NewConfiguration.SqliteDatabasePath = AppPathHelper.Instance.SqliteDbDefaultPath;
                ConfigurationService = new ConfigurationService(KeywordMatchService, NewConfiguration);
            }
            // make sure path is not empty
            if (string.IsNullOrWhiteSpace(NewConfiguration.SqliteDatabasePath))
            {
                NewConfiguration.SqliteDatabasePath = AppPathHelper.Instance.SqliteDbDefaultPath;
            }

            ConfigurationService.SetSelfStart();
            ThemeService = new ThemeService(App.ResourceDictionary, ConfigurationService.Theme);
            GlobalData = new GlobalData(ConfigurationService);
        }

        private bool _isNewUser = false;
        private EnumDbStatus _localDataConnectionStatus;

        public void InitOnConfigure()
        {
            IoC.Get<LanguageService>().SetLanguage(IoC.Get<ConfigurationService>().General.CurrentLanguageCode);

            // Init data sources controller
            var dataSourceService = IoC.Get<DataSourceService>();
            GlobalData.SetDataSourceService(dataSourceService);
            _localDataConnectionStatus = dataSourceService.InitLocalDataSource();
            IoC.Get<GlobalData>().ReloadServerList();
            foreach (var config in ConfigurationService!.AdditionalDataSource)
            {
                dataSourceService.AddOrUpdateDataSourceAsync(config);
            }
            IoC.Get<SessionControlService>();
        }


        public void InitOnLaunch()
        {
            KittyConfig.CleanUpOldConfig();

            if (_localDataConnectionStatus != EnumDbStatus.OK)
            {
                string error = _localDataConnectionStatus.GetErrorInfo();
                MessageBox.Show(error, IoC.Get<LanguageService>().Translate("Error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                IoC.Get<MainWindowViewModel>().ShowMe(goPage: EnumMainWindowPage.SettingsData);
                return;
            }

            if (_isNewUser && PRemoteMTransferHelper.IsNeedTransfer())
            {
                // import form PRemoteM db
                IoC.Get<MainWindowViewModel>().OnMainWindowViewLoaded += PRemoteMTransferHelper.TransAsync;
            }

            if (IoC.Get<ConfigurationService>().General.AppStartMinimized == false || _isNewUser)
            {
                IoC.Get<MainWindowViewModel>().ShowMe(goPage: EnumMainWindowPage.List);
            }

            if (_isNewUser == false && ConfigurationService != null)
            {
                MsAppCenterHelper.TraceSpecial($"App start with - ListPageIsCardView", $"{ConfigurationService.General.ListPageIsCardView}");
                MsAppCenterHelper.TraceSpecial($"App start with - ConfirmBeforeClosingSession", $"{ConfigurationService.General.ConfirmBeforeClosingSession}");
                MsAppCenterHelper.TraceSpecial($"App start with - LauncherEnabled}}", $"{ConfigurationService.Launcher.LauncherEnabled}");
                MsAppCenterHelper.TraceSpecial($"App start with - Theme}}", $"{ConfigurationService.Theme.ThemeName}");
            }
        }
    }
}
