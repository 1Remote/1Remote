using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using _1RM.Model;
using _1RM.Model.DAO;
using _1RM.Model.DAO.Dapper;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.Utils;
using com.github.xiangyuecn.rsacsharp;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.FileSystem;
using Stylet;

namespace _1RM.View.Settings.DataSource
{
    public class SqliteSettingViewModel : NotifyPropertyChangedBaseScreen
    {
        public SqliteSource? OrgSqliteConfig { get; } = null;
        private readonly string _orgRsaPublicKey;
        private readonly string _orgRsaPrivateKeyPath;

        public readonly SqliteSource New = new SqliteSource();
        private readonly DataSourceViewModel _dataSourceViewModel;

        public SqliteSettingViewModel(DataSourceViewModel dataSourceViewModel, SqliteSource? sqliteConfig = null)
        {
            OrgSqliteConfig = sqliteConfig;
            _dataSourceViewModel = dataSourceViewModel;
            _orgRsaPublicKey = "";
            _orgRsaPrivateKeyPath = "";

            // Edit mode
            if (OrgSqliteConfig != null)
            {
                Name = OrgSqliteConfig.DataSourceName;
                Path = OrgSqliteConfig.Path;
                _orgRsaPublicKey = DbRsaPublicKey = OrgSqliteConfig.Database_GetPublicKey() ?? "";
                _orgRsaPrivateKeyPath = DbRsaPrivateKeyPath = OrgSqliteConfig.Database_GetPrivateKeyPath() ?? "";
                New.Database_SelfCheck();
                // disable name editing of LocalSource
                if (dataSourceViewModel.LocalSource == sqliteConfig)
                {
                    NameWritable = false;
                }
                OrgSqliteConfig?.Database_CloseConnection();
            }
        }
        ~SqliteSettingViewModel()
        {
        }

        protected override void OnViewLoaded()
        {
            GlobalEventHelper.ShowProcessingRing += ShowProcessingRing;
        }

        protected override void OnClose()
        {
            GlobalEventHelper.ShowProcessingRing -= ShowProcessingRing;
            New.Database_CloseConnection();
        }

        private INotifyPropertyChanged? _topLevelViewModel;
        public INotifyPropertyChanged? TopLevelViewModel
        {
            get => _topLevelViewModel;
            set => SetAndNotifyIfChanged(ref _topLevelViewModel, value);
        }

        private void ShowProcessingRing(Visibility visibility, string msg)
        {
            Execute.OnUIThread(() =>
            {
                if (visibility == Visibility.Visible)
                {
                    var pvm = IoC.Get<ProcessingRingViewModel>();
                    pvm.ProcessingRingMessage = msg;
                    this.TopLevelViewModel = pvm;
                }
                else
                {
                    this.TopLevelViewModel = null;
                }
            });
        }

        private bool ValidateDbStatusAndShowMessageBox(bool showAlert = true)
        {
            if (New.Database_OpenConnection())
            {
                DbRsaPublicKey = New.Database_GetPublicKey() ?? "";
                DbRsaPrivateKeyPath = New.Database_GetPrivateKeyPath() ?? "";
            }
            // validate rsa key
            var res = New.Database_SelfCheck();
            if (res == EnumDbStatus.OK)
            {
                return true;
            }
            if (showAlert == false) return false;
            MessageBoxHelper.ErrorAlert(res.GetErrorInfo());
            return false;
        }


        public bool NameWritable { get; } = true;
        private string _name = "";
        public string Name
        {
            get => _name;
            set
            {
                if (NameWritable && SetAndNotifyIfChanged(ref _name, value))
                {
                    New.DataSourceName = value;
                    if (string.IsNullOrWhiteSpace(_name))
                        throw new ArgumentException(IoC.Get<ILanguageService>().Translate("Can not be empty!"));
                    if (_dataSourceViewModel.SourceConfigs.Any(x => x != OrgSqliteConfig && string.Equals(x.DataSourceName, _name, StringComparison.CurrentCultureIgnoreCase)))
                        throw new ArgumentException(IoC.Get<ILanguageService>().Translate("{0} is existed!", _name));
                }
            }
        }

        private string _path = "";
        public string Path
        {
            get => _path;
            set
            {
                if (SetAndNotifyIfChanged(ref _path, value))
                {
                    New.Database_CloseConnection();
                    New.Path = value;
                    //if (string.IsNullOrEmpty(Path) == false && File.Exists(Path))
                    //{
                    //    New.Database_OpenConnection();
                    //}
                }
            }
        }

        private string _dbRsaPublicKey = "";
        public string DbRsaPublicKey
        {
            get => _dbRsaPublicKey;
            private set => SetAndNotifyIfChanged(ref _dbRsaPublicKey, value);
        }
        private string _dbRsaPrivateKeyPath = "";
        public string DbRsaPrivateKeyPath
        {
            get => _dbRsaPrivateKeyPath;
            set => SetAndNotifyIfChanged(ref _dbRsaPrivateKeyPath, value);
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
                }, o => string.IsNullOrWhiteSpace(DbRsaPrivateKeyPath));
            }
        }

        /// <summary>
        /// Invoke Progress bar percent = arg1 / arg2
        /// </summary>
        private void OnRsaProgress(bool stop)
        {
            GlobalEventHelper.ShowProcessingRing?.Invoke(stop ? Visibility.Collapsed : Visibility.Visible, IoC.Get<ILanguageService>().Translate("system_options_data_security_info_data_processing"));
        }

        private const string PrivateKeyFileExt = ".pem";
        public void GenRsa(string privateKeyPath = "")
        {
            if (New.Status == EnumDbStatus.AccessDenied)
                return;

            if (string.IsNullOrEmpty(privateKeyPath))
            {
                var path = SelectFileHelper.OpenFile(
                    checkFileExists: false,
                    initialDirectory: AppPathHelper.Instance.BaseDirPath,
                    title: IoC.Get<LanguageService>().Translate("system_options_data_security_rsa_encrypt_dialog_title"),
                    selectedFileName: DateTime.Now.ToString("yyyyMMddhhmmss") + PrivateKeyFileExt,
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
                    Debug.Assert(File.Exists(Path));
                    File.Copy(Path, Path + ".back", true);

                    string privateKeyContent = "";
                    if (File.Exists(privateKeyPath))
                    {
                        privateKeyContent = File.ReadAllText(privateKeyPath);
                    }
                    else
                    {
                        // gen rsa
                        var rsa = new RSA(2048);
                        privateKeyContent = rsa.ToPEM_PKCS1();
                        // save key file
                        File.WriteAllText(privateKeyPath, privateKeyContent);
                    }

                    var ss = New.GetServers().Select(x => x.Server);
                    if (New.Database_SetEncryptionKey(privateKeyPath, privateKeyContent, ss) != RSA.EnumRsaStatus.NoError)
                    {
                        MessageBoxHelper.ErrorAlert(EnumDbStatus.RsaPrivateKeyFormatError.GetErrorInfo());
                        OnRsaProgress(true);
                        return;
                    }

                    // del back up
                    File.Delete(Path + ".back");

                    DbRsaPublicKey = privateKeyContent;
                    DbRsaPrivateKeyPath = privateKeyPath;

                    // done
                    OnRsaProgress(true);
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
                }, o => string.IsNullOrWhiteSpace(DbRsaPrivateKeyPath) == false);
            }
        }
        public void CleanRsa()
        {
            if (New.Status != EnumDbStatus.OK)
                return;

            Task.Factory.StartNew(() =>
            {
                OnRsaProgress(false);
                lock (this)
                {
                    // database back up
                    Debug.Assert(File.Exists(Path));
                    File.Copy(Path, Path + ".back", true);

                    var ss = New.GetServers().Select(x => x.Server);
                    if (New.Database_SetEncryptionKey("", "", ss) != RSA.EnumRsaStatus.NoError)
                    {
                        MessageBoxHelper.ErrorAlert(EnumDbStatus.RsaPrivateKeyFormatError.GetErrorInfo());
                        OnRsaProgress(true);
                        return;
                    }

                    // del key
                    //File.Delete(ppkPath);

                    // del back up
                    File.Delete(Path + ".back");

                    DbRsaPublicKey = "";
                    DbRsaPrivateKeyPath = "";

                    // done
                    OnRsaProgress(true);

                    _clearingRsa = false;
                }
            });
        }



        private RelayCommand? _cmdSelectRsaPrivateKey;
        public RelayCommand CmdSelectRsaPrivateKey
        {
            get
            {
                return _cmdSelectRsaPrivateKey ??= new RelayCommand((o) =>
                {
                    if (string.IsNullOrEmpty(DbRsaPrivateKeyPath)) return;
                    lock (this)
                    {
                        var path = SelectFileHelper.OpenFile(
                            initialDirectory: new FileInfo(DbRsaPrivateKeyPath).DirectoryName,
                            filter: $"PRM RSA private key|*{PrivateKeyFileExt}");
                        if (path == null) return;
                        var pks = RSA.CheckPrivatePublicKeyMatch(path, DbRsaPublicKey);
                        if (pks == RSA.EnumRsaStatus.NoError)
                        {
                            // update private key only
                            New.Database_UpdatePrivateKeyPathOnly(path);
                            DbRsaPrivateKeyPath = path;
                        }
                        else
                        {
                            MessageBoxHelper.ErrorAlert(EnumDbStatus.RsaNotMatched.GetErrorInfo());
                        }
                    }
                }, o => string.IsNullOrWhiteSpace(DbRsaPrivateKeyPath) == false);
            }
        }


        private RelayCommand? _cmdSelectDbPath;
        public RelayCommand CmdSelectDbPath
        {
            get
            {
                return _cmdSelectDbPath ??= new RelayCommand((o) =>
                {
                    var newPath = SelectFileHelper.OpenFile(
                        initialDirectory: string.IsNullOrWhiteSpace(Path) == false && File.Exists(Path) ? new FileInfo(Path).DirectoryName : "",
                        filter: "SqliteSource Database|*.db",
                        checkFileExists: false);
                    if (newPath == null) return;
                    var oldPath = Path;
                    if (string.Equals(newPath, oldPath, StringComparison.CurrentCultureIgnoreCase))
                        return;

                    if (!IoPermissionHelper.HasWritePermissionOnFile(newPath))
                    {
                        MessageBoxHelper.ErrorAlert(IoC.Get<ILanguageService>().Translate("string_database_error_permission_denied"));
                        return;
                    }
                    OnRsaProgress(false);
                    Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            Path = newPath;
                            if (New.Database_OpenConnection())
                            {
                                DbRsaPublicKey = New.Database_GetPublicKey() ?? "";
                                DbRsaPrivateKeyPath = New.Database_GetPrivateKeyPath() ?? "";
                            }

                            ValidateDbStatusAndShowMessageBox();
                        }
                        catch (Exception ee)
                        {
                            Path = oldPath;
                            SimpleLogHelper.Warning(ee);
                            MessageBoxHelper.ErrorAlert(IoC.Get<ILanguageService>().Translate("system_options_data_security_error_can_not_open"));
                        }
                        OnRsaProgress(true);
                    });
                });
            }
        }


        private RelayCommand? _cmdSave;
        public RelayCommand CmdSave
        {
            get
            {
                return _cmdSave ??= new RelayCommand((o) =>
                {
                    if (ValidateDbStatusAndShowMessageBox() == false)
                        return;
                    var ret = false;
                    if (OrgSqliteConfig != null)
                    {
                        if (Name != OrgSqliteConfig.DataSourceName
                            || OrgSqliteConfig.Path != Path
                            || _orgRsaPublicKey != DbRsaPublicKey
                            || _orgRsaPrivateKeyPath != DbRsaPrivateKeyPath)
                        {
                            ret = true;
                        }
                        OrgSqliteConfig.DataSourceName = Name;
                        OrgSqliteConfig.Path = Path;
                    }
                    else
                    {
                        ret = true;
                    }

                    this.RequestClose(ret);

                }, o => (
                    string.IsNullOrWhiteSpace(Name) == false
                    && string.IsNullOrWhiteSpace(Path) == false
                    && File.Exists(Path)));
            }
        }



        private RelayCommand? _cmdCancel;
        public RelayCommand CmdCancel
        {
            get
            {
                return _cmdCancel ??= new RelayCommand((o) =>
                {
                    New.Database_CloseConnection();
                    this.RequestClose(false);
                }, o => OrgSqliteConfig == null);
            }
        }
    }
}
