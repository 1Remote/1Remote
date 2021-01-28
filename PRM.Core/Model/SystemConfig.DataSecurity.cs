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
using PRM.Core.DB.freesql;
using PRM.Core.Protocol;
using PRM.Core.Protocol.Putty.SSH;
using PRM.Core.Protocol.RDP;
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

        /// <summary>
        /// Check if db is existed and writable
        /// </summary>
        /// <returns>
        /// Tuple(is decrypted, error info)
        /// </returns>
        public Tuple<bool, string> CheckIfDbIsWritable()
        {
            var path = SystemConfig.Instance.DataSecurity.DbPath;
            try
            {
                var fi = new FileInfo(SystemConfig.Instance.DataSecurity.DbPath);
                if (!Directory.Exists(fi.DirectoryName))
                    Directory.CreateDirectory(fi.DirectoryName);
                if (IOPermissionHelper.HasWritePermissionOnFile(path))
                {
                    return new Tuple<bool, string>(true, "");
                }
                else
                {
                    return new Tuple<bool, string>(false, SystemConfig.Instance.Language.GetText("string_permission_denied") + $": {path}");
                }
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
                return new Tuple<bool, string>(false, e.Message);
            }
        }

        /// <summary>
        /// Check if db is(can) decrypted by the private key
        /// </summary>
        /// <returns>
        /// Tuple(is decrypted, error info)
        /// </returns>
        public Tuple<bool, string> CheckIfDbIsOk(string rsaPrivateKeyPath = "")
        {
            var c1 = CheckIfDbIsWritable();
            if (!c1.Item1)
                return c1;

            var ret = _context.DbOperator.CheckDbRsaIsOk();
            switch (ret)
            {
                case -1:
                    return new Tuple<bool, string>(false, SystemConfig.Instance.Language.GetText("system_options_data_security_error_rsa_private_key_format_error"));

                case -2:
                    return new Tuple<bool, string>(false, SystemConfig.Instance.Language.GetText("system_options_data_security_error_rsa_private_key_not_found"));

                case -3:
                    return new Tuple<bool, string>(false, SystemConfig.Instance.Language.GetText("system_options_data_security_error_rsa_private_key_not_match"));
            }
            return new Tuple<bool, string>(true, "");
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

        public string RsaPublicKey => this._context.Db?.Get_RSA_PublicKey();
        public string RsaPrivateKeyPath => this._context.Db?.Get_RSA_PrivateKeyPath();

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
            var res = SystemConfig.Instance.DataSecurity.CheckIfDbIsOk();
            if (!res.Item1)
            {
                MessageBox.Show(res.Item2, SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                return;
            }

            var t = new Task(() =>
            {
                lock (_lockerForRsa)
                {
                    if (!string.IsNullOrEmpty(_context.DbOperator.GetRsaPrivateKeyPath())) return;

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

                    var result = _context.DbOperator.SetRsaPrivateKey(dlg.FileName);
                    switch (result)
                    {
                        case -1:
                            break;

                        case -2:
                            break;

                        case -3:
                            break;

                        default:
                            break;
                    }

                    // encrypt old data
                    foreach (var vmProtocolServer in this._context.AppData.VmItemList)
                    {
                        OnRsaProgress(++val, max);
                        this._context.DbOperator.EncryptPwdIfItIsNotEncrypted(vmProtocolServer.Server);
                        var tmp = (ProtocolServerBase)vmProtocolServer.Server.Clone();
                        this._context.DbOperator.EncryptInfo(tmp);
                        this._context.Db.UpdateServer(tmp);
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
            // validate rsa key
            var res = SystemConfig.Instance.DataSecurity.CheckIfDbIsOk();
            if (!res.Item1)
            {
                MessageBox.Show(res.Item2, SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                return;
            }
            var rsaPublicKey = this._context.Db.Get_RSA_PublicKey();

            var t = new Task(() =>
                {
                    if (string.IsNullOrEmpty(rsaPublicKey)) return;
                    lock (_lockerForRsa)
                    {
                        if (string.IsNullOrEmpty(rsaPublicKey)) return;
                        OnRsaProgress(0, 1);
                        int max = this._context.AppData.VmItemList.Count() * 3 + 2 + 1;
                        int val = 1;
                        OnRsaProgress(val, max);

                        // database back up
                        Debug.Assert(File.Exists(DbPath));
                        File.Copy(_dbPath, _dbPath + ".back", true);
                        OnRsaProgress(++val, max);

                        // keep pld rsa
                        //var ppkPath = this.Db.Get_RSA_PrivateKeyPath();
                        //var rsa = new RSA(File.ReadAllText(this.Db.Get_RSA_PrivateKeyPath()), true);

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
                            this._context.Db.UpdateServer(vmProtocolServer.Server);
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
                        var dlg = new OpenFileDialog
                        {
                            Filter = $"private key|*{SystemConfigDataSecurity.PrivateKeyFileExt}",
                            InitialDirectory = new FileInfo(RsaPrivateKeyPath).DirectoryName,
                        };
                        if (dlg.ShowDialog() == true)
                        {
                            var res = CheckIfDbIsOk(dlg.FileName);
                            if (!res.Item1)
                            {
                                MessageBox.Show(res.Item2, SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                            }
                        }
                    });
                }
                return _cmdSelectRsaPrivateKey;
            }
        }

        private RelayCommand _cmdGenRsaKey;

        public RelayCommand CmdGenRsaKey
        {
            get
            {
                return _cmdGenRsaKey ??= new RelayCommand((o) => { GenRsa(); });
            }
        }

        private RelayCommand _cmdClearRsaKey;

        public RelayCommand CmdClearRsaKey
        {
            get
            {
                return _cmdClearRsaKey ??= new RelayCommand((o) =>
                {
                    var res = CheckIfDbIsOk();
                    if (!res.Item1)
                    {
                        MessageBox.Show(res.Item2, SystemConfig.Instance.Language.GetText("messagebox_title_error"),
                            MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                        if (MessageBoxResult.Yes == MessageBox.Show(
                            SystemConfig.Instance.Language.GetText("system_options_data_security_info_clear_rsa"),
                            SystemConfig.Instance.Language.GetText("messagebox_title_warning"),
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning, MessageBoxResult.None))
                        {
                            if (File.Exists(DbPath))
                                File.Delete(DbPath);
                            this._context.AppData.ServerListUpdate();
                            Load();
                        }
                    }
                    else
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
                    if (dlg.ShowDialog() == true)
                    {
                        var path = dlg.FileName;
                        var oldDbPath = DbPath;
                        try
                        {
                            if (IOPermissionHelper.HasWritePermissionOnFile(path))
                            {
                                this._context.Db.OpenConnection(DatabaseType.Sqlite, FreeSqlDb.GetConnectionStringSqlite(path));
                                this._context.AppData.ServerListUpdate();
                                DbPath = path;
                            }
                            // TODO more detail info for db read error.
                            else
                                MessageBox.Show(
                                    SystemConfig.Instance.Language.GetText(
                                        "system_options_data_security_error_can_not_open"),
                                    SystemConfig.Instance.Language.GetText("messagebox_title_error"),
                                    MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                        }
                        catch (Exception ee)
                        {
                            DbPath = oldDbPath;
                            SimpleLogHelper.Warning(ee);
                            MessageBox.Show(
                                SystemConfig.Instance.Language.GetText(
                                    "system_options_data_security_error_can_not_open"),
                                SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK,
                                MessageBoxImage.Error, MessageBoxResult.None);
                        }
                    }
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
                    if (dlg.ShowDialog() == true)
                    {
                        var path = dlg.FileName;
                        var oldDbPath = DbPath;
                        if (oldDbPath == path)
                            return;
                        try
                        {
                            if (IOPermissionHelper.HasWritePermissionOnFile(path))
                            {
                                this._context.Db.CloseConnection();
                                File.Move(oldDbPath, path);
                                File.Delete(oldDbPath);
                                this._context.Db.OpenConnection(DatabaseType.Sqlite, FreeSqlDb.GetConnectionStringSqlite(path));
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
                            SimpleLogHelper.Debug(ee);
                            DbPath = oldDbPath;
                            MessageBox.Show(
                                SystemConfig.Instance.Language.GetText(
                                    "system_options_data_security_error_can_not_open"),
                                SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK,
                                MessageBoxImage.Error, MessageBoxResult.None);
                        }
                    }
                });
            }
        }

        #endregion CMD
    }
}