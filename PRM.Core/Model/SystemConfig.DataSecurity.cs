using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.IO;
using com.github.xiangyuecn.rsacsharp;
using Microsoft.Win32;
using PRM.Core.DB;
using PRM.Core.DB.IDB;
using Shawn.Utils;

namespace PRM.Core.Model
{
    public sealed class SystemConfigDataSecurity : SystemConfigBase
    {
        private readonly PrmContext _context;

        public SystemConfigDataSecurity(PrmContext context, Ini ini) : base(ini)
        {
            _context = context;
            Load();
            // restore from back. (in these case .back is existed ---- when data encrypt/decrypt processing throw exception)
            if (File.Exists(_dbPath + ".back"))
            {
                File.Copy(_dbPath + ".back", _dbPath, true);
                File.Delete(_dbPath + ".back");
            }
        }

        private bool ValidateDbStatusAndShowMessageBox()
        {
            // validate rsa key
            var res = _context.DbOperator.CheckDbRsaStatus();
            RaisePropertyChanged(nameof(RsaPublicKey));
            RaisePropertyChanged(nameof(RsaPrivateKeyPath));
            if (res != EnumDbStatus.OK)
            {
                MessageBox.Show(res.GetErrorInfo(SystemConfig.Instance.Language, DbPath), SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                return false;
            }
            return true;
        }

        private string _dbPath = null;

        public string DbPath
        {
            get
            {
                if (!string.IsNullOrEmpty(_dbPath)) return _dbPath;
                var appDateFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), SystemConfig.AppName);
                if (!Directory.Exists(appDateFolder))
                    Directory.CreateDirectory(appDateFolder);
                _dbPath = Path.Combine(appDateFolder, $"{SystemConfig.AppName}.db");
                Save();
                return _dbPath;
            }
            private set
            {
                lock (_lockerForRsa)
                {
                    SetAndNotifyIfChanged(nameof(DbPath), ref _dbPath, value.Replace(Environment.CurrentDirectory, "."));
                    RaisePropertyChanged(nameof(RsaPublicKey));
                    RaisePropertyChanged(nameof(RsaPrivateKeyPath));
                }
            }
        }

        public string RsaPublicKey => this._context.DbOperator.GetRsaPublicKey();
        public string RsaPrivateKeyPath => this._context.DbOperator.GetRsaPrivateKeyPath();

        /// <summary>
        /// Invoke Progress bar percent = arg1 / arg2
        /// </summary>
        private void OnRsaProgress(int now, int total)
        {
            GlobalEventHelper.OnLongTimeProgress?.Invoke(now, total, SystemConfig.Instance.Language.GetText("system_options_data_security_info_data_processing"));
        }

        private readonly object _lockerForRsa = new object();
        private const string PrivateKeyFileExt = ".prpk";

        private void GenRsa()
        {
            // validate rsa key
            Debug.Assert(_context.DbOperator.IsDbEncrypted() == false);
            var t = new Task(() =>
            {
                lock (_lockerForRsa)
                {
                    if (_context.DbOperator.IsDbEncrypted()) return;
                    var dlg = new OpenFileDialog
                    {
                        Title = SystemConfig.Instance.Language.GetText("system_options_data_security_rsa_encrypt_dialog_title"),
                        Filter = $"PRM RSA private key|*{PrivateKeyFileExt}",
                        FileName = SystemConfig.AppName + "_" + DateTime.Now.ToString("yyyyMMddhhmmss") + PrivateKeyFileExt,
                        CheckFileExists = false,
                    };
                    if (dlg.ShowDialog() != true) return;

                    int max = this._context.AppData.VmItemList.Count() * 3 + 2;
                    int val = 0;
                    OnRsaProgress(val, max);
                    // database back up
                    Debug.Assert(File.Exists(DbPath));
                    File.Copy(_dbPath, _dbPath + ".back", true);
                    OnRsaProgress(++val, max);

                    if (!File.Exists(dlg.FileName))
                    {
                        // gen rsa
                        var rsa = new RSA(2048);
                        // save key file
                        File.WriteAllText(dlg.FileName, rsa.ToPEM_PKCS1());
                    }

                    OnRsaProgress(++val, max);

                    if (_context.DbOperator.SetRsaPrivateKey(dlg.FileName) < 0)
                    {
                        MessageBox.Show(EnumDbStatus.RsaPrivateKeyFormatError.GetErrorInfo(SystemConfig.Instance.Language, DbPath), SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                        OnRsaProgress(0, 0);
                        return;
                    }

                    // encrypt old data
                    foreach (var vmProtocolServer in this._context.AppData.VmItemList)
                    {
                        OnRsaProgress(++val, max);
                        this._context.DbOperator.DbUpdateServer(vmProtocolServer.Server);
                        OnRsaProgress(++val, max);
                    }

                    // del back up
                    File.Delete(_dbPath + ".back");

                    // done
                    OnRsaProgress(0, 0);

                    RaisePropertyChanged(nameof(RsaPublicKey));
                    RaisePropertyChanged(nameof(RsaPrivateKeyPath));
                }
            });
            t.Start();
        }

        private void CleanRsa()
        {
            Debug.Assert(_context.DbOperator.IsDbEncrypted() == true);
            var t = new Task(() =>
                {
                    lock (_lockerForRsa)
                    {
                        if (!_context.DbOperator.IsDbEncrypted()) return;
                        OnRsaProgress(0, 1);
                        int max = this._context.AppData.VmItemList.Count() * 3 + 2 + 1;
                        int val = 1;
                        OnRsaProgress(val, max);

                        // database back up
                        Debug.Assert(File.Exists(DbPath));
                        File.Copy(_dbPath, _dbPath + ".back", true);
                        OnRsaProgress(++val, max);

                        // decrypt pwd
                        foreach (var vmProtocolServer in this._context.AppData.VmItemList)
                        {
                            this._context.DbOperator.DecryptPwdIfItIsEncrypted(vmProtocolServer.Server);
                            OnRsaProgress(++val, max);
                        }

                        // remove rsa keys from db
                        this._context.DbOperator.SetRsaPrivateKey("");

                        // update db
                        foreach (var vmProtocolServer in this._context.AppData.VmItemList)
                        {
                            this._context.DbOperator.DbUpdateServer(vmProtocolServer.Server);
                            OnRsaProgress(++val, max);
                        }

                        // del key
                        //File.Delete(ppkPath);

                        // del back up
                        File.Delete(_dbPath + ".back");

                        // done
                        OnRsaProgress(0, 0);

                        RaisePropertyChanged(nameof(RsaPublicKey));
                        RaisePropertyChanged(nameof(RsaPrivateKeyPath));
                    }
                });
            t.Start();
        }

        #region Interface

        private const string SectionName = "DataSecurity";

        public override void Save()
        {
            _ini.WriteValue(nameof(DbPath).ToLower(), SectionName, DbPath);
            _ini.Save();
        }

        public override void Load()
        {
            StopAutoSave = true;
            DbPath = ReadDbPath(_ini);
            StopAutoSave = false;
        }

        public static string ReadDbPath(Ini ini)
        {
            string ret = ini.GetValue(nameof(DbPath).ToLower(), SectionName);
            return ret;
        }

        public override void Update(SystemConfigBase newConfig)
        {
            UpdateBase(this, newConfig, typeof(SystemConfigDataSecurity));
            Save();
        }

        #endregion Interface

        #region CMD

        private RelayCommand _cmdSelectRsaPrivateKey;

        public RelayCommand CmdSelectRsaPrivateKey
        {
            get
            {
                if (_cmdSelectRsaPrivateKey == null)
                {
                    _cmdSelectRsaPrivateKey = new RelayCommand((o) =>
                    {
                        if (!_context.DbOperator.IsDbEncrypted())
                        {
                            return;
                        }
                        var dlg = new OpenFileDialog
                        {
                            Filter = $"private key|*{SystemConfigDataSecurity.PrivateKeyFileExt}",
                            InitialDirectory = new FileInfo(RsaPrivateKeyPath).DirectoryName,
                        };
                        if (dlg.ShowDialog() != true) return;
                        var res = _context.DbOperator.VerifyRsaPrivateKey(dlg.FileName);
                        if (res)
                        {
                            _context.DbOperator.SetRsaPrivateKey(dlg.FileName);
                        }
                        else
                        {
                            MessageBox.Show(EnumDbStatus.RsaNotMatched.GetErrorInfo(SystemConfig.Instance.Language, DbPath), SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                        }
                    }, o => _context.DbOperator.IsDbEncrypted());
                }
                return _cmdSelectRsaPrivateKey;
            }
        }

        private RelayCommand _cmdGenRsaKey;

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
                    if (_context.DbOperator.IsDbEncrypted())
                    {
                        return;
                    }
                    GenRsa();
                });
            }
        }

        private RelayCommand _cmdClearRsaKey;

        public RelayCommand CmdClearRsaKey
        {
            get
            {
                return _cmdClearRsaKey ??= new RelayCommand((o) =>
                {
                    // validate rsa key
                    if (!ValidateDbStatusAndShowMessageBox())
                    {
                        return;
                    }
                    if (!_context.DbOperator.IsDbEncrypted())
                    {
                        return;
                    }
                    CleanRsa();
                });
            }
        }

        private RelayCommand _cmdSelectDbPath;

        public RelayCommand CmdSelectDbPath
        {
            get
            {
                return _cmdSelectDbPath ??= new RelayCommand((o) =>
                {
                    var dlg = new OpenFileDialog
                    {
                        Filter = "Sqlite Database|*.db",
                        CheckFileExists = false,
                        InitialDirectory = new FileInfo(DbPath).DirectoryName
                    };
                    if (dlg.ShowDialog() != true) return;

                    var path = dlg.FileName;
                    var oldDbPath = DbPath;
                    if (string.Equals(path, oldDbPath, StringComparison.CurrentCultureIgnoreCase))
                        return;

                    if (IOPermissionHelper.IsFileInUse(path) || !IOPermissionHelper.HasWritePermissionOnFile(path))
                    {
                        MessageBox.Show(SystemConfig.Instance.Language.GetText("system_options_data_security_error_can_not_open"), SystemConfig.Instance.Language.GetText("messagebox_title_error"),
                            MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                        return;
                    }

                    try
                    {
                        this._context.DbOperator.OpenConnection(DatabaseType.Sqlite, DbExtensions.GetSqliteConnectionString(path));
                        this._context.AppData.ServerListUpdate();
                    }
                    catch (Exception ee)
                    {
                        path = oldDbPath;
                        this._context.DbOperator.OpenConnection(DatabaseType.Sqlite, DbExtensions.GetSqliteConnectionString(path));
                        this._context.AppData.ServerListUpdate();
                        SimpleLogHelper.Warning(ee);
                        MessageBox.Show(
                            SystemConfig.Instance.Language.GetText(
                                "system_options_data_security_error_can_not_open"),
                            SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK,
                            MessageBoxImage.Error, MessageBoxResult.None);
                    }

                    DbPath = path;
                    ValidateDbStatusAndShowMessageBox();
                });
            }
        }

        private RelayCommand _cmdDbMigrate;

        public RelayCommand CmdDbMigrate
        {
            get
            {
                return _cmdDbMigrate ??= new RelayCommand((o) =>
                {
                    var dlg = new SaveFileDialog
                    {
                        Filter = "Sqlite Database|*.db",
                        CheckFileExists = false,
                        InitialDirectory = new FileInfo(DbPath).DirectoryName,
                        FileName = new FileInfo(DbPath).Name
                    };
                    if (dlg.ShowDialog() != true) return;
                    var path = dlg.FileName;
                    var oldDbPath = DbPath;
                    if (oldDbPath == path)
                        return;
                    try
                    {
                        if (!IOPermissionHelper.IsFileInUse(path)
                            && IOPermissionHelper.HasWritePermissionOnFile(path))
                        {
                            this._context.DbOperator.CloseConnection();
                            File.Move(oldDbPath, path);
                            File.Delete(oldDbPath);
                            this._context.DbOperator.OpenConnection(DatabaseType.Sqlite, DbExtensions.GetSqliteConnectionString(path));
                            // Migrate do not need reload data
                            // this._appContext.AppData.ServerListUpdate();
                            DbPath = path;
                        }
                        else
                            MessageBox.Show(
                                SystemConfig.Instance.Language.GetText(
                                    "system_options_data_security_error_can_not_open"),
                                SystemConfig.Instance.Language.GetText("messagebox_title_error"),
                                MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                    }
                    catch (Exception ee)
                    {
                        SimpleLogHelper.Error(ee);
                        DbPath = oldDbPath;
                        MessageBox.Show(
                            SystemConfig.Instance.Language.GetText(
                                "system_options_data_security_error_can_not_open"),
                            SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK,
                            MessageBoxImage.Error, MessageBoxResult.None);
                    }
                });
            }
        }

        #endregion CMD
    }
}