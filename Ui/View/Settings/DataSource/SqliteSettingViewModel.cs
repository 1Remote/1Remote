using System;
using System.Collections.Generic;
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

namespace _1RM.View.Settings.DataSource
{
    public class SqliteSettingViewModel : NotifyPropertyChangedBase
    {
        public SqliteConfig SqliteConfig { get; }
        private SqliteDatabaseSource _databaseSource;
        private readonly ILanguageService _languageService = IoC.Get<ILanguageService>();
        public SqliteSettingViewModel(SqliteConfig sqliteConfig)
        {
            SqliteConfig = sqliteConfig;
            _databaseSource = new SqliteDatabaseSource("tmp", sqliteConfig);
            ValidateDbStatusAndShowMessageBox(false);
        }

        private bool ValidateDbStatusAndShowMessageBox(bool showAlert = true)
        {
            // validate rsa key
            var res = _databaseSource.Database_SelfCheck();
            if (res == EnumDbStatus.OK)
            {
                DbRsaPublicKey = _databaseSource.Database_GetPublicKey() ?? "";
                DbRsaPrivateKeyPath = _databaseSource.Database_GetPrivateKeyPath() ?? "";
                return true;
            }
            if (showAlert == false) return true;
            MessageBoxHelper.ErrorAlert(res.GetErrorInfo());
            return false;
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
                    Debug.Assert(File.Exists(SqliteConfig.Path));
                    File.Copy(SqliteConfig.Path, SqliteConfig.Path + ".back", true);

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


                    var ss = _databaseSource.GetServers().Select(x => x.Server);
                    if (_databaseSource.Database_SetEncryptionKey(privateKeyPath, privateKeyContent, ss) != RSA.EnumRsaStatus.NoError)
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
                    File.Delete(SqliteConfig.Path + ".back");

                    // done
                    OnRsaProgress(true);

                    ValidateDbStatusAndShowMessageBox();

                    // TODO 重新读取数据
                    //_appData.ReloadServerList();
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
                });
            }
        }
        public Task CleanRsa()
        {
            var t = new Task(() =>
            {
                OnRsaProgress(false);
                lock (this)
                {
                    // database back up
                    Debug.Assert(File.Exists(SqliteConfig.Path));
                    File.Copy(SqliteConfig.Path, SqliteConfig.Path + ".back", true);

                    var ss = _databaseSource.GetServers().Select(x => x.Server);
                    if (_databaseSource.Database_SetEncryptionKey("", "", ss) != RSA.EnumRsaStatus.NoError)
                    {
                        MessageBoxHelper.ErrorAlert(EnumDbStatus.RsaPrivateKeyFormatError.GetErrorInfo());
                        OnRsaProgress(true);
                        return;
                    }

                    // del key
                    //File.Delete(ppkPath);

                    // del back up
                    File.Delete(SqliteConfig.Path + ".back");

                    ValidateDbStatusAndShowMessageBox();

                    // TODO 重新读取数据
                    //_appData.ReloadServerList();

                    // done
                    OnRsaProgress(true);

                    _clearingRsa = false;
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
                    if (string.IsNullOrEmpty(DbRsaPrivateKeyPath)) return;
                    lock (this)
                    {
                        var path = SelectFileHelper.OpenFile(
                            initialDirectory: new FileInfo(DbRsaPrivateKeyPath).DirectoryName,
                            filter: $"PRM RSA private key|*{PrivateKeyFileExt}");
                        if (path == null) return;
                        var pks = RSA.CheckPrivatePublicKeyMatch(path, _databaseSource.Database_GetPublicKey());
                        if (pks == RSA.EnumRsaStatus.NoError)
                        {
                            // update private key only
                            _databaseSource.Database_UpdatePrivateKeyPathOnly(path);
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
                        initialDirectory: new FileInfo(SqliteConfig.Path).DirectoryName,
                        filter: "Sqlite Database|*.db");
                    if (path == null) return;
                    var oldPath = SqliteConfig.Path;
                    if (string.Equals(path, oldPath, StringComparison.CurrentCultureIgnoreCase))
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
                            SqliteConfig.Path = path;
                            _databaseSource.Database_CloseConnection();
                            _databaseSource = new SqliteDatabaseSource("", SqliteConfig);
                            //_databaseSource.DataSourceConfig = SqliteConfig;
                            //_databaseSource.Database_OpenConnection();
                            //_dataSourceService.InitLocalDataSource(path);
                            //_appData.ReloadServerList();
                            //_configurationService.DataSource.LocalDatabasePath = path;
                            //_configurationService.Save();
                            ValidateDbStatusAndShowMessageBox();
                        }
                        catch (Exception ee)
                        {
                            SqliteConfig.Path = oldPath;
                            _databaseSource.Database_CloseConnection();
                            _databaseSource = new SqliteDatabaseSource("", SqliteConfig);
                            //_configurationService.DataSource.LocalDatabasePath = oldPath;
                            //_dataSourceService.InitLocalDataSource(oldPath);
                            SimpleLogHelper.Warning(ee);
                            MessageBoxHelper.ErrorAlert(_languageService.Translate("system_options_data_security_error_can_not_open"));
                        }
                        OnRsaProgress(true);
                    });
                });
            }
        }
    }
}
