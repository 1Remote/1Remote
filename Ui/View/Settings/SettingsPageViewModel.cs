using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using com.github.xiangyuecn.rsacsharp;
using _1RM.Model;
using _1RM.Model.DAO;
using _1RM.Service;
using _1RM.Utils;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.FileSystem;
using _1RM.Service.DataSource;

namespace _1RM.View.Settings
{
    public class SettingsPageViewModel : NotifyPropertyChangedBaseScreen
    {
        private readonly DataSourceService _sourceService;
        private LanguageService _languageService => IoC.Get<LanguageService>();
        private LauncherService _launcherService => IoC.Get<LauncherService>();
        private ThemeService _themeService => IoC.Get<ThemeService>();
        private ConfigurationService _configurationService => IoC.Get<ConfigurationService>();
        private readonly GlobalData _appData;


        public SettingsPageViewModel(DataSourceService sourceService, GlobalData appData)
        {
            _sourceService = sourceService;
            _appData = appData;
            _sourceService.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(DataSourceService.DataService))
                    ValidateDbStatusAndShowMessageBox(false);
            };
            ValidateDbStatusAndShowMessageBox(false);
        }

        protected override void OnViewLoaded()
        {
            ShowPage(_initPage);
        }

        public void SetLanguage(string languageCode = "")
        {
            if (string.IsNullOrEmpty(languageCode) == false
                && _languageService.LanguageCode2Name.ContainsKey(languageCode)
                && _languageService.SetLanguage(languageCode))
            {
                _configurationService.General.CurrentLanguageCode = languageCode;
            }
        }


        private EnumMainWindowPage _initPage = EnumMainWindowPage.SettingsGeneral;
        public void ShowPage(EnumMainWindowPage page)
        {
            if (this.View is SettingsPageView view)
            {
                switch (page)
                {
                    case EnumMainWindowPage.SettingsGeneral:
                        view.TabItemGeneral.IsSelected = true;
                        break;
                    case EnumMainWindowPage.SettingsData:
                        view.TabItemDataBase.IsSelected = true;
                        break;
                    case EnumMainWindowPage.SettingsRunners:
                        view.TabItemRunners.IsSelected = true;
                        break;
                    case EnumMainWindowPage.SettingsLauncher:
                        view.TabItemLauncher.IsSelected = true;
                        break;
                    case EnumMainWindowPage.SettingsTheme:
                        view.TabItemTheme.IsSelected = true;
                        break;
                    case EnumMainWindowPage.List:
                    case EnumMainWindowPage.About:
                    default:
                        CmdSaveAndGoBack.Execute();
                        break;
                }
            }
            else
            {
                _initPage = page;
            }
        }


        private Visibility _progressBarVisibility = Visibility.Collapsed;
        public Visibility ProgressBarVisibility
        {
            get => _progressBarVisibility;
            private set => SetAndNotifyIfChanged(ref _progressBarVisibility, value);
        }


        private RelayCommand? _cmdSaveAndGoBack;
        public RelayCommand CmdSaveAndGoBack
        {
            get
            {
                return _cmdSaveAndGoBack ??= new RelayCommand((o) =>
                {
                    // check if Db is ok
                    var res = _sourceService.DataService?.Database_SelfCheck() ?? EnumDbStatus.AccessDenied;
                    if (res != EnumDbStatus.OK)
                    {
                        MessageBoxHelper.ErrorAlert(res.GetErrorInfo());
                        return;
                    }


                    _configurationService.Save();
                    _sourceService.ProtocolConfigurationService.Save();
                    IoC.Get<MainWindowViewModel>().ShowList();
                });
            }
        }

        private RelayCommand? _cmdOpenPath;
        public RelayCommand CmdOpenPath
        {
            get
            {
                if (_cmdOpenPath != null) return _cmdOpenPath;
                _cmdOpenPath = new RelayCommand((o) =>
                {
                    var path = o?.ToString() ?? "";
                    if (File.Exists(path))
                    {
                        System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("Explorer.exe")
                        {
                            Arguments = "/e,/select," + path
                        };
                        System.Diagnostics.Process.Start(psi);
                    }

                    if (Directory.Exists(path))
                    {
                        System.Diagnostics.Process.Start("explorer.exe", path);
                    }
                });
                return _cmdOpenPath;
            }
        }







        public Dictionary<string, string> Languages => _languageService.LanguageCode2Name;
        public string Language
        {
            get => _configurationService.General.CurrentLanguageCode;
            set
            {
                Debug.Assert(Languages.ContainsKey(value));
                if (SetAndNotifyIfChanged(ref _configurationService.General.CurrentLanguageCode, value))
                {
                    // reset lang service
                    _languageService.SetLanguage(value);
                    _configurationService.Save();
                }
            }
        }

        public bool AppStartAutomatically
        {
            get => _configurationService.General.AppStartAutomatically;
            set
            {
                if (SetAndNotifyIfChanged(ref _configurationService.General.AppStartAutomatically, value))
                {
                    _configurationService.Save();
                }
            }
        }

        public bool AppStartMinimized
        {
            get => _configurationService.General.AppStartMinimized;
            set
            {
                if (SetAndNotifyIfChanged(ref _configurationService.General.AppStartMinimized, value))
                {
                    _configurationService.Save();
                }
            }
        }

        public bool ListPageIsCardView
        {
            get => _configurationService.General.ListPageIsCardView;
            set
            {
                if (SetAndNotifyIfChanged(ref _configurationService.General.ListPageIsCardView, value))
                {
                    _configurationService.Save();
                }
            }
        }

        public bool ConfirmBeforeClosingSession
        {
            get => _configurationService.General.ConfirmBeforeClosingSession;
            set
            {
                if (SetAndNotifyIfChanged(ref _configurationService.General.ConfirmBeforeClosingSession, value))
                {
                    _configurationService.Save();
                }
            }
        }



        public bool LauncherEnabled
        {
            get => _configurationService.Launcher.LauncherEnabled;
            set
            {
                if (SetAndNotifyIfChanged(ref _configurationService.Launcher.LauncherEnabled, value))
                {
                    _configurationService.Save();
                    GlobalEventHelper.OnLauncherHotKeyChanged?.Invoke();
                }
            }
        }

        public HotkeyModifierKeys LauncherHotKeyModifiers
        {
            get => _configurationService.Launcher.HotKeyModifiers;
            set
            {
                if (value != LauncherHotKeyModifiers
                    && _launcherService.CheckIfHotkeyAvailable(value, LauncherHotKeyKey)
                    && SetAndNotifyIfChanged(ref _configurationService.Launcher.HotKeyModifiers, value))
                {
                    _configurationService.Save();
                    GlobalEventHelper.OnLauncherHotKeyChanged?.Invoke();
                }
            }
        }

        public Key LauncherHotKeyKey
        {
            get => _configurationService.Launcher.HotKeyKey;
            set
            {
                if (value != LauncherHotKeyKey
                    && _launcherService.CheckIfHotkeyAvailable(LauncherHotKeyModifiers, value)
                    && SetAndNotifyIfChanged(ref _configurationService.Launcher.HotKeyKey, value))
                {
                    _configurationService.Save();
                    GlobalEventHelper.OnLauncherHotKeyChanged?.Invoke();
                }
            }
        }

        public string? LogFolderName => new FileInfo(SimpleLogHelper.LogFileName).DirectoryName;

        public List<MatchProviderInfo> AvailableMatcherProviders => _configurationService.AvailableMatcherProviders;

        #region Database

        public string DbPath => _configurationService.Database.SqliteDatabasePath;

        private string _dbRsaPublicKey = "";
        public string DbRsaPublicKey
        {
            get => _dbRsaPublicKey;
            set => SetAndNotifyIfChanged(ref _dbRsaPublicKey, value);
        }
        private string _dbRsaPrivateKeyPath = "";
        public string DbRsaPrivateKeyPath
        {
            get => _dbRsaPrivateKeyPath;
            set => SetAndNotifyIfChanged(ref _dbRsaPrivateKeyPath, value);
        }


        private bool ValidateDbStatusAndShowMessageBox(bool showAlert = true)
        {
            // validate rsa key
            var res = (_sourceService.DataService?.Database_SelfCheck()) ?? EnumDbStatus.NotConnected;
            DbRsaPublicKey = _sourceService.DataService?.Database_GetPublicKey() ?? "";
            DbRsaPrivateKeyPath = _sourceService.DataService?.Database_GetPrivateKeyPath() ?? "";
            if (res == EnumDbStatus.OK) return true;
            if (showAlert == false) return true;
            MessageBoxHelper.ErrorAlert(res.GetErrorInfo());
            return false;
        }

        private RelayCommand? _cmdGenRsaKey;
        public RelayCommand CmdGenRsaKey
        {
            get
            {
                return _cmdGenRsaKey ??= new RelayCommand((o) =>
                {
                    // validate rsa key
                    if (!ValidateDbStatusAndShowMessageBox())
                    {
                        return;
                    }
                    if (string.IsNullOrEmpty(DbRsaPrivateKeyPath) == true)
                    {
                        GenRsa();
                    }
                });
            }
        }

        /// <summary>
        /// Invoke Progress bar percent = arg1 / arg2
        /// </summary>
        private void OnRsaProgress(bool stop)
        {
            GlobalEventHelper.ShowProcessingRing?.Invoke(stop ? Visibility.Collapsed : Visibility.Visible, _languageService.Translate("system_options_data_security_info_data_processing"));
        }

        private const string PrivateKeyFileExt = ".pem";
        public void GenRsa(string privateKeyPath = "")
        {
            if (_sourceService.DataService == null) return;
            if (string.IsNullOrEmpty(privateKeyPath))
            {
                var path = SelectFileHelper.OpenFile(
                    selectedFileName: AppPathHelper.APP_DISPLAY_NAME + "_" + DateTime.Now.ToString("yyyyMMddhhmmss") + PrivateKeyFileExt,
                    checkFileExists: false,
                    initialDirectory: AppPathHelper.Instance.BaseDirPath,
                    filter: $"RSA private key|*{PrivateKeyFileExt}");
                if (path == null) return;
                privateKeyPath = path;
            }

            // validate rsa key
            var t = new Task(() =>
            {
                lock (this)
                {
                    OnRsaProgress(false);
                    // database back up
                    Debug.Assert(File.Exists(DbPath));
                    File.Copy(DbPath, DbPath + ".back", true);

                    string privateKeyContent = "";
                    if (!File.Exists(privateKeyPath))
                    {
                        // gen rsa
                        var rsa = new RSA(2048);
                        privateKeyContent = rsa.ToPEM_PKCS1();
                    }
                    else
                    {
                        privateKeyContent = File.ReadAllText(privateKeyPath);
                    }

                    var ss = _appData.VmItemList.Select(x => x.Server);
                    if (_sourceService.DataService.Database_SetEncryptionKey(privateKeyPath, privateKeyContent, ss) != RSA.EnumRsaStatus.NoError)
                    {
                        MessageBoxHelper.ErrorAlert(EnumDbStatus.RsaPrivateKeyFormatError.GetErrorInfo());
                        OnRsaProgress(true);
                        return;
                    }


                    if (!File.Exists(privateKeyPath))
                    {
                        // save key file
                        File.WriteAllText(privateKeyPath, privateKeyContent);
                    }

                    // del back up
                    File.Delete(DbPath + ".back");

                    // done
                    OnRsaProgress(true);

                    ValidateDbStatusAndShowMessageBox();

                    _appData.ReloadServerList();
                }
            });
            t.Start();
        }


        private bool _clearingRsa = false;
        private RelayCommand? _cmdClearRsaKey;
        public RelayCommand CmdClearRsaKey
        {
            get
            {
                return _cmdClearRsaKey ??= new RelayCommand((o) =>
                {
                    if (_sourceService.DataService == null) return;
                    if (_clearingRsa) return;
                    _clearingRsa = true;
                    // validate rsa key
                    if (!ValidateDbStatusAndShowMessageBox())
                    {
                        return;
                    }
                    if (string.IsNullOrEmpty(DbRsaPrivateKeyPath) != true)
                    {
                        CleanRsa();
                    }
                });
            }
        }
        public Task CleanRsa()
        {
            var t = new Task(() =>
            {
                if (_sourceService.DataService == null) return;
                OnRsaProgress(false);
                lock (this)
                {
                    // database back up
                    Debug.Assert(File.Exists(DbPath));
                    File.Copy(DbPath, DbPath + ".back", true);

                    var ss = _appData.VmItemList.Select(x => x.Server);
                    if (_sourceService.DataService.Database_SetEncryptionKey("", "", ss) != RSA.EnumRsaStatus.NoError)
                    {
                        MessageBoxHelper.ErrorAlert(EnumDbStatus.RsaPrivateKeyFormatError.GetErrorInfo());
                        OnRsaProgress(true);
                        return;
                    }

                    // del key
                    //File.Delete(ppkPath);

                    // del back up
                    File.Delete(DbPath + ".back");

                    ValidateDbStatusAndShowMessageBox();
                    _appData.ReloadServerList();
                    // done
                    OnRsaProgress(true);

                    _clearingRsa = true;
                }
            });
            t.Start();
            return t;
        }



        private RelayCommand? _cmdSelectRsaPrivateKey;
        public RelayCommand CmdSelectRsaPrivateKey
        {
            get
            {
                return _cmdSelectRsaPrivateKey ??= new RelayCommand((o) =>
                {
                    if (_sourceService.DataService == null) return;
                    if (string.IsNullOrEmpty(DbRsaPrivateKeyPath)) return;
                    lock (this)
                    {
                        var path = SelectFileHelper.OpenFile(
                            initialDirectory: new FileInfo(DbRsaPrivateKeyPath).DirectoryName,
                            filter: $"PRM RSA private key|*{PrivateKeyFileExt}");
                        if (path == null) return;
                        var pks = RSA.CheckPrivatePublicKeyMatch(path, _sourceService.DataService.Database_GetPublicKey());
                        if (pks == RSA.EnumRsaStatus.NoError)
                        {
                            // update private key only
                            _sourceService.DataService.Database_UpdatePrivateKeyPathOnly(path);
                            ValidateDbStatusAndShowMessageBox();
                        }
                        else
                        {
                            MessageBoxHelper.ErrorAlert(EnumDbStatus.RsaNotMatched.GetErrorInfo());
                        }
                    }
                });
            }
        }


        private RelayCommand? _cmdSelectDbPath;
        public RelayCommand CmdSelectDbPath
        {
            get
            {
                return _cmdSelectDbPath ??= new RelayCommand((o) =>
                {
                    var path = SelectFileHelper.OpenFile(
                        initialDirectory: new FileInfo(DbPath).DirectoryName,
                        filter: "Sqlite Database|*.db");
                    if (path == null) return;
                    var oldDbPath = DbPath;
                    if (string.Equals(path, oldDbPath, StringComparison.CurrentCultureIgnoreCase))
                        return;

                    if (!IoPermissionHelper.HasWritePermissionOnFile(path))
                    {
                        MessageBoxHelper.ErrorAlert(_languageService.Translate("string_database_error_permission_denied"));
                        return;
                    }

                    OnRsaProgress(false);
                    Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            _sourceService.InitSqliteDb(path);
                            _appData.ReloadServerList();
                            _configurationService.Database.SqliteDatabasePath = path;
                            RaisePropertyChanged(nameof(DbPath));
                            _configurationService.Save();
                            ValidateDbStatusAndShowMessageBox();
                        }
                        catch (Exception ee)
                        {
                            _configurationService.Database.SqliteDatabasePath = oldDbPath;
                            _sourceService.InitSqliteDb(oldDbPath);
                            SimpleLogHelper.Warning(ee);
                            MessageBoxHelper.ErrorAlert(_languageService.Translate("system_options_data_security_error_can_not_open"));
                        }
                        OnRsaProgress(true);
                    });
                });
            }
        }



        private RelayCommand? _cmdDbMigrate;
        public RelayCommand CmdDbMigrate
        {
            get
            {
                return _cmdDbMigrate ??= new RelayCommand((o) =>
                {
                    if (_sourceService.DataService == null) return;
                    var path = SelectFileHelper.SaveFile(filter: "Sqlite Database|*.db", initialDirectory: new FileInfo(DbPath).DirectoryName, selectedFileName: new FileInfo(DbPath).Name);
                    if (path == null) return;
                    var oldDbPath = DbPath;
                    if (oldDbPath == path)
                        return;
                    try
                    {
                        if (IoPermissionHelper.HasWritePermissionOnFile(path))
                        {
                            this._sourceService.DataService.Database_CloseConnection();
                            File.Copy(oldDbPath, path);
                            Thread.Sleep(500);
                            this._sourceService.DataService.Database_OpenConnection(DatabaseType.Sqlite, DbExtensions.GetSqliteConnectionString(path));
                            // Migrate do not need reload data
                            // this._app_appData.ReloadServerList();
                            _configurationService.Database.SqliteDatabasePath = path;
                            File.Delete(oldDbPath);
                        }
                        else
                            MessageBoxHelper.ErrorAlert(_languageService.Translate("system_options_data_security_error_can_not_open"));
                    }
                    catch (Exception ee)
                    {
                        SimpleLogHelper.Error(ee);
                        File.Delete(path);
                        _configurationService.Database.SqliteDatabasePath = oldDbPath;
                        this._sourceService.DataService.Database_OpenConnection(DatabaseType.Sqlite, DbExtensions.GetSqliteConnectionString(oldDbPath));
                        MessageBoxHelper.ErrorAlert(_languageService.Translate("system_options_data_security_error_can_not_open"));
                    }
                    RaisePropertyChanged(nameof(DbPath));
                    _configurationService.Save();
                });
            }
        }


        #endregion


        #region UI

        private void SetTheme(string name)
        {
            Debug.Assert(_themeService.Themes.ContainsKey(name));
            var theme = _themeService.Themes[name];
            _configurationService.Theme.PrimaryMidColor = theme.PrimaryMidColor;
            _configurationService.Theme.PrimaryLightColor = theme.PrimaryLightColor;
            _configurationService.Theme.PrimaryDarkColor = theme.PrimaryDarkColor;
            _configurationService.Theme.PrimaryTextColor = theme.PrimaryTextColor;
            _configurationService.Theme.AccentMidColor = theme.AccentMidColor;
            _configurationService.Theme.AccentLightColor = theme.AccentLightColor;
            _configurationService.Theme.AccentDarkColor = theme.AccentDarkColor;
            _configurationService.Theme.AccentTextColor = theme.AccentTextColor;
            _configurationService.Theme.BackgroundColor = theme.BackgroundColor;
            _configurationService.Theme.BackgroundTextColor = theme.BackgroundTextColor;

            RaisePropertyChanged(nameof(PrimaryMidColor));
            RaisePropertyChanged(nameof(PrimaryLightColor));
            RaisePropertyChanged(nameof(PrimaryDarkColor));
            RaisePropertyChanged(nameof(PrimaryTextColor));
            RaisePropertyChanged(nameof(AccentMidColor));
            RaisePropertyChanged(nameof(AccentLightColor));
            RaisePropertyChanged(nameof(AccentDarkColor));
            RaisePropertyChanged(nameof(AccentTextColor));
            RaisePropertyChanged(nameof(BackgroundColor));
            RaisePropertyChanged(nameof(BackgroundTextColor));

            _themeService.ApplyTheme(_configurationService.Theme);
        }

        public string ThemeName
        {
            get => _configurationService.Theme.ThemeName;
            set
            {
                Debug.Assert(_themeService.Themes.ContainsKey(value));
                if (SetAndNotifyIfChanged(ref _configurationService.Theme.ThemeName, value))
                {
                    SetTheme(value);
                    _configurationService.Save();
                }
            }
        }

        public List<string> ThemeList => _themeService.Themes.Select(x => x.Key).ToList();


        public string PrimaryMidColor
        {
            get => _configurationService.Theme.PrimaryMidColor;
            set
            {
                try
                {
                    if (SetAndNotifyIfChanged(ref _configurationService.Theme.PrimaryMidColor, value))
                    {
                        var color = ColorAndBrushHelper.HexColorToMediaColor(value);
                        _configurationService.Theme.PrimaryLightColor = System.Drawing.ColorTranslator.ToHtml(System.Drawing.Color.FromArgb(Math.Min(color.R + 50, 255), Math.Min(color.G + 45, 255), Math.Min(color.B + 40, 255)));
                        _configurationService.Theme.PrimaryDarkColor = System.Drawing.ColorTranslator.ToHtml(System.Drawing.Color.FromArgb((int)(color.R * 0.8), (int)(color.G * 0.8), (int)(color.B * 0.8)));
                        RaisePropertyChanged(nameof(PrimaryLightColor));
                        RaisePropertyChanged(nameof(PrimaryDarkColor));
                        _themeService.ApplyTheme(_configurationService.Theme);
                    }
                }
                catch (Exception e)
                {
                    SimpleLogHelper.Debug(e);
                }
            }
        }
        public string PrimaryLightColor
        {
            get => _configurationService.Theme.PrimaryLightColor;
            set
            {
                SetAndNotifyIfChanged(ref _configurationService.Theme.PrimaryLightColor, value);
                _themeService.ApplyTheme(_configurationService.Theme);
            }
        }

        public string PrimaryDarkColor
        {
            get => _configurationService.Theme.PrimaryDarkColor;
            set
            {
                SetAndNotifyIfChanged(ref _configurationService.Theme.PrimaryDarkColor, value);
                _themeService.ApplyTheme(_configurationService.Theme);
            }
        }

        public string PrimaryTextColor
        {
            get => _configurationService.Theme.PrimaryTextColor;
            set
            {
                SetAndNotifyIfChanged(ref _configurationService.Theme.PrimaryTextColor, value);
                _themeService.ApplyTheme(_configurationService.Theme);
            }
        }

        public string AccentMidColor
        {
            get => _configurationService.Theme.AccentMidColor;
            set
            {
                try
                {
                    if (SetAndNotifyIfChanged(ref _configurationService.Theme.AccentMidColor, value))
                    {
                        var color = ColorAndBrushHelper.HexColorToMediaColor(value);
                        _configurationService.Theme.AccentLightColor = System.Drawing.ColorTranslator.ToHtml(System.Drawing.Color.FromArgb(Math.Min(color.R + 50, 255), Math.Min(color.G + 45, 255), Math.Min(color.B + 40, 255)));
                        _configurationService.Theme.AccentDarkColor = System.Drawing.ColorTranslator.ToHtml(System.Drawing.Color.FromArgb((int)(color.R * 0.8), (int)(color.G * 0.8), (int)(color.B * 0.8)));
                        RaisePropertyChanged(nameof(AccentLightColor));
                        RaisePropertyChanged(nameof(AccentDarkColor));
                        _themeService.ApplyTheme(_configurationService.Theme);
                    }
                }
                catch (Exception e)
                {
                    SimpleLogHelper.Debug(e);
                }
            }
        }
        public string AccentLightColor
        {
            get => _configurationService.Theme.AccentLightColor;
            set
            {
                SetAndNotifyIfChanged(ref _configurationService.Theme.AccentLightColor, value);
                _themeService.ApplyTheme(_configurationService.Theme);
            }
        }

        public string AccentDarkColor
        {
            get => _configurationService.Theme.AccentDarkColor;
            set
            {
                SetAndNotifyIfChanged(ref _configurationService.Theme.AccentDarkColor, value);
                _themeService.ApplyTheme(_configurationService.Theme);
            }
        }

        public string AccentTextColor
        {
            get => _configurationService.Theme.AccentTextColor;
            set
            {
                SetAndNotifyIfChanged(ref _configurationService.Theme.AccentTextColor, value);
                _themeService.ApplyTheme(_configurationService.Theme);
            }
        }

        public string BackgroundColor
        {
            get => _configurationService.Theme.BackgroundColor;
            set
            {
                SetAndNotifyIfChanged(ref _configurationService.Theme.BackgroundColor, value);
                _themeService.ApplyTheme(_configurationService.Theme);
            }
        }

        public string BackgroundTextColor
        {
            get => _configurationService.Theme.BackgroundTextColor;
            set
            {
                SetAndNotifyIfChanged(ref _configurationService.Theme.BackgroundTextColor, value);
                _themeService.ApplyTheme(_configurationService.Theme);
            }
        }


        private RelayCommand? _cmdPrmThemeReset;
        public RelayCommand CmdResetTheme
        {
            get
            {
                return _cmdPrmThemeReset ??= new RelayCommand((o) =>
                {
                    SetTheme(ThemeName);
                    _configurationService.Save();
                    _themeService.ApplyTheme(_configurationService.Theme);
                });
            }
        }
        #endregion
    }
}
