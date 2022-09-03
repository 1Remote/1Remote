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
using _1RM.Model.DAO.Dapper;
using _1RM.Service;
using _1RM.Utils;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.FileSystem;
using _1RM.Service.DataSource;

namespace _1RM.View.Settings
{
    public partial class SettingsPageViewModel : NotifyPropertyChangedBaseScreen
    {
        #region LocalDatabase

        public string DbPath => _configurationService.DataSource.LocalDatabasePath;

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


        private bool ValidateDbStatusAndShowMessageBox(bool showAlert = true)
        {
            // validate rsa key
            var res = (_dataSourceService.LocalDataSource?.Database_SelfCheck()) ?? EnumDbStatus.NotConnected;
            if (res == EnumDbStatus.OK)
            {
                DbRsaPublicKey = _dataSourceService.LocalDataSource?.Database_GetPublicKey() ?? "";
                DbRsaPrivateKeyPath = _dataSourceService.LocalDataSource?.Database_GetPrivateKeyPath() ?? "";
                return true;
            }
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
            if (_dataSourceService.LocalDataSource == null) return;
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
                    if (_dataSourceService.LocalDataSource.Database_SetEncryptionKey(privateKeyPath, privateKeyContent, ss) != RSA.EnumRsaStatus.NoError)
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
                    if (_dataSourceService.LocalDataSource == null) return;
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
                if (_dataSourceService.LocalDataSource == null) return;
                OnRsaProgress(false);
                lock (this)
                {
                    // database back up
                    Debug.Assert(File.Exists(DbPath));
                    File.Copy(DbPath, DbPath + ".back", true);

                    var ss = _appData.VmItemList.Select(x => x.Server);
                    if (_dataSourceService.LocalDataSource.Database_SetEncryptionKey("", "", ss) != RSA.EnumRsaStatus.NoError)
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
                    if (_dataSourceService.LocalDataSource == null) return;
                    if (string.IsNullOrEmpty(DbRsaPrivateKeyPath)) return;
                    lock (this)
                    {
                        var path = SelectFileHelper.OpenFile(
                            initialDirectory: new FileInfo(DbRsaPrivateKeyPath).DirectoryName,
                            filter: $"PRM RSA private key|*{PrivateKeyFileExt}");
                        if (path == null) return;
                        var pks = RSA.CheckPrivatePublicKeyMatch(path, _dataSourceService.LocalDataSource.Database_GetPublicKey());
                        if (pks == RSA.EnumRsaStatus.NoError)
                        {
                            // update private key only
                            _dataSourceService.LocalDataSource.Database_UpdatePrivateKeyPathOnly(path);
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
                            _dataSourceService.InitLocalDataSource(path);
                            _appData.ReloadServerList();
                            _configurationService.DataSource.LocalDatabasePath = path;
                            RaisePropertyChanged(nameof(DbPath));
                            _configurationService.Save();
                            ValidateDbStatusAndShowMessageBox();
                        }
                        catch (Exception ee)
                        {
                            _configurationService.DataSource.LocalDatabasePath = oldDbPath;
                            _dataSourceService.InitLocalDataSource(oldDbPath);
                            SimpleLogHelper.Warning(ee);
                            MessageBoxHelper.ErrorAlert(_languageService.Translate("system_options_data_security_error_can_not_open"));
                        }
                        OnRsaProgress(true);
                    });
                });
            }
        }



        private RelayCommand? _cmdLocalDatabaseMigrate;
        public RelayCommand CmdLocalDatabaseMigrate
        {
            get
            {
                return _cmdLocalDatabaseMigrate ??= new RelayCommand((o) =>
                {
                    if (_dataSourceService.LocalDataSource == null) return;
                    var path = SelectFileHelper.SaveFile(filter: "Sqlite Database|*.db", initialDirectory: new FileInfo(DbPath).DirectoryName, selectedFileName: new FileInfo(DbPath).Name);
                    if (path == null) return;
                    var oldDbPath = DbPath;
                    if (oldDbPath == path)
                        return;
                    try
                    {
                        if (IoPermissionHelper.HasWritePermissionOnFile(path))
                        {
                            this._dataSourceService.LocalDataSource.Database_CloseConnection();
                            File.Copy(oldDbPath, path);
                            Thread.Sleep(500);
                            var db = new DapperDataBaseFree();
                            db.OpenNewConnection(DatabaseType.Sqlite, DbExtensions.GetSqliteConnectionString(path));
                            if (db.IsConnected())
                            {
                                db.CloseConnection();
                                this._dataSourceService.InitLocalDataSource(path);
                            }
                            // Migrate do not need to reload data
                            // this._app_appData.ReloadServerList();
                            _configurationService.DataSource.LocalDatabasePath = path;
                            File.Delete(oldDbPath);
                        }
                        else
                            MessageBoxHelper.ErrorAlert(_languageService.Translate("system_options_data_security_error_can_not_open"));
                    }
                    catch (Exception ee)
                    {
                        SimpleLogHelper.Error(ee);
                        if (File.Exists(path))
                            File.Delete(path);
                        _configurationService.DataSource.LocalDatabasePath = oldDbPath;
                        MessageBoxHelper.ErrorAlert(_languageService.Translate("system_options_data_security_error_can_not_open"));
                    }

                    this._dataSourceService.LocalDataSource.Database_OpenConnection();
                    RaisePropertyChanged(nameof(DbPath));
                    _configurationService.Save();
                });
            }
        }


        #endregion
    }
}
