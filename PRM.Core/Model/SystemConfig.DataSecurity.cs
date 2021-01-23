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
        private readonly GlobalData _appData;
        public SystemConfigDataSecurity(GlobalData appData, Ini ini) : base(ini)
        {
            _appData = appData;
            Load();
            // restore from back. (in these case .back will existed: data encrypt/decrypt processing throw exception)
            if (File.Exists(_dbPath + ".back"))
            {
                File.Copy(_dbPath + ".back", _dbPath, true);
                File.Delete(_dbPath + ".back");
            }
            _db = new FreeSqlDb(DatabaseType.Sqlite, FreeSqlDb.GetConnectionStringSqlite(DbPath));
        }

        private readonly IDb _db;

        /// <summary>
        /// Check if db is existed and writable
        /// </summary>
        /// <returns>
        /// Tuple(is decrypted, error info)
        /// </returns>
        private static Tuple<bool, string> CheckIfDbIsWritable()
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

            var sha1 = this._db.Get_RSA_SHA1();
            var rsaPublicKey = this._db.Get_RSA_PublicKey();
            if (string.IsNullOrEmpty(rsaPrivateKeyPath))
                rsaPrivateKeyPath = this._db.Get_RSA_PrivateKeyPath();

            // NO RSA
            if (string.IsNullOrEmpty(sha1)
                && string.IsNullOrEmpty(rsaPrivateKeyPath)
                && string.IsNullOrEmpty(rsaPublicKey))
                return new Tuple<bool, string>(true, "");

            if (!File.Exists(rsaPrivateKeyPath))
                return new Tuple<bool, string>(false, SystemConfig.Instance.Language.GetText("system_options_data_security_error_rsa_private_key_not_found"));

            RSA rsaPublicKeyObj = null;
            RSA rsaPrivateKeyObj = null;

            try
            {
                rsaPrivateKeyObj = new RSA(File.ReadAllText(rsaPrivateKeyPath), true);
            }
            catch (Exception)
            {
                return new Tuple<bool, string>(false, SystemConfig.Instance.Language.GetText("system_options_data_security_error_rsa_private_key_not_match"));
            }

            // make sure public key is PEM format key
            try
            {
                rsaPublicKeyObj = new RSA(rsaPublicKey, true);
            }
            catch (Exception)
            {
                // try to fix public key
                if (rsaPrivateKeyObj.Verify("SHA1", sha1, SystemConfig.AppName))
                {
                    this._db.Set_RSA_PublicKey(rsaPrivateKeyObj.ToPEM_PKCS1(true));
                    rsaPublicKeyObj = new RSA(File.ReadAllText(rsaPrivateKeyPath), true);
                }
            }


            // check if RSA private key is matched public key?
            try
            {
                rsaPrivateKeyObj = new RSA(File.ReadAllText(rsaPrivateKeyPath), true);
                var sha1Tmp = rsaPrivateKeyObj.Sign("SHA1", SystemConfig.AppName);
                if (rsaPublicKeyObj?.Verify("SHA1", sha1Tmp, SystemConfig.AppName) != true)
                {
                    throw new Exception("RSA key is not match!");
                }
                this._db.Set_RSA_SHA1(sha1Tmp);
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
                return new Tuple<bool, string>(false, SystemConfig.Instance.Language.GetText("system_options_data_security_error_rsa_private_key_not_match"));
            }
            return new Tuple<bool, string>(true, "");
        }

        private string _dbPath = null;
        public string DbPath
        {
            get
            {
                if (string.IsNullOrEmpty(_dbPath))
                {
                    var appDateFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), SystemConfig.AppName);
                    if (!Directory.Exists(appDateFolder))
                        Directory.CreateDirectory(appDateFolder);
                    _dbPath = Path.Combine(appDateFolder, $"{SystemConfig.AppName}.db");
                    Save();
                }
                return _dbPath;
            }
            private set
            {
                lock (_lockerForRsa)
                {
                    SetAndNotifyIfChanged(nameof(DbPath), ref _dbPath, value.Replace(Environment.CurrentDirectory, "."));
                    Rsa = null;
                    RaisePropertyChanged(nameof(RsaPublicKey));
                    RaisePropertyChanged(nameof(RsaPrivateKeyPath));
                }
            }
        }

        public string RsaPublicKey => this._db.Get_RSA_PublicKey();
        public string RsaPrivateKeyPath => this._db.Get_RSA_PrivateKeyPath();

        private RSA _rsa = null;
        public RSA Rsa
        {
            set => _rsa = value;
            get
            {
                if (_rsa == null)
                {
                    var ret = CheckIfDbIsOk();
                    if (!ret.Item1)
                    {
                        throw new Exception(ret.Item2);
                    }
                    if (string.IsNullOrEmpty(this._db.Get_RSA_PublicKey()))
                        return null;
                    _rsa = new RSA(File.ReadAllText(this._db.Get_RSA_PrivateKeyPath()), true);
                }
                return _rsa;
            }
        }

        /// <summary>
        /// Invoke Progress bar percent = arg1 / arg2
        /// </summary>
        private void OnRsaProgress(int now, int total)
        {
            GlobalEventHelper.OnLongTimeProgress?.Invoke(now, total, SystemConfig.Instance.Language.GetText("system_options_data_security_info_data_processing"));
        }



        private readonly object _lockerForRsa = new object();
        public const string PrivateKeyFileExt = ".prpk";
        public void GenRsa()
        {
            // validate rsa key
            var res = SystemConfig.Instance.DataSecurity.CheckIfDbIsOk();
            if (!res.Item1)
            {
                MessageBox.Show(res.Item2, SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                return;
            }

            var rsaPublicKey = this._db.Get_RSA_PublicKey();


            var t = new Task(() =>
            {
                if (!string.IsNullOrEmpty(rsaPublicKey)) return;
                lock (_lockerForRsa)
                {
                    if (!string.IsNullOrEmpty(rsaPublicKey)) return;

                    int max = _appData.VmItemList.Count() * 3 + 2;
                    int val = 0;

                    var dlg = new OpenFileDialog
                    {
                        Title = SystemConfig.Instance.Language.GetText("system_options_data_security_rsa_encrypt_dialog_title"),
                        Filter = $"PRM RSA private key|*{PrivateKeyFileExt}",
                        FileName = SystemConfig.AppName + "_" + DateTime.Now.ToString("yyyyMMddhhmmss") + PrivateKeyFileExt,
                        CheckFileExists = false,
                    };
                    if (dlg.ShowDialog() == true)
                    {
                        OnRsaProgress(val, max);
                        // database back up
                        Debug.Assert(File.Exists(DbPath));
                        File.Copy(_dbPath, _dbPath + ".back", true);
                        OnRsaProgress(++val, max);

                        Rsa = null;
                        RSA rsa = null;
                        // try read rsa
                        if (File.Exists(dlg.FileName))
                        {
                            try
                            {
                                rsa = new RSA(File.ReadAllText(dlg.FileName), true);
                            }
                            catch (Exception e)
                            {
                                SimpleLogHelper.Debug(e);
                                rsa = null;
                            }
                        }
                        // gen rsa
                        if (rsa == null)
                        {
                            rsa = new RSA(2048);
                            // save key file
                            File.WriteAllText(dlg.FileName, rsa.ToPEM_PKCS1());
                        }
                        OnRsaProgress(++val, max);

                        // key write to db
                        this._db.Set_RSA_SHA1(rsa.Sign("SHA1", SystemConfig.AppName));
                        this._db.Set_RSA_PublicKey(rsa.ToPEM_PKCS1(true));
                        this._db.Set_RSA_PrivateKeyPath(dlg.FileName);
                        RaisePropertyChanged(nameof(RsaPublicKey));
                        RaisePropertyChanged(nameof(RsaPrivateKeyPath));

                        // encrypt old data
                        foreach (var vmProtocolServer in _appData.VmItemList)
                        {
                            OnRsaProgress(++val, max);
                            this.EncryptPwdIfItIsNotEncrypted(vmProtocolServer.Server);
                            var tmp = (ProtocolServerBase)vmProtocolServer.Server.Clone();
                            this.EncryptInfo(tmp);
                            this._db.UpdateServer(tmp);
                            OnRsaProgress(++val, max);
                        }

                        // del back up
                        File.Delete(_dbPath + ".back");

                        // done
                        OnRsaProgress(0, 0);
                    }
                }
            });
            t.Start();

        }
        public void CleanRsa()
        {
            // validate rsa key
            var res = SystemConfig.Instance.DataSecurity.CheckIfDbIsOk();
            if (!res.Item1)
            {
                MessageBox.Show(res.Item2, SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                return;
            }
            var rsaPublicKey = this._db.Get_RSA_PublicKey();

            var t = new Task(() =>
                {
                    if (string.IsNullOrEmpty(rsaPublicKey)) return;
                    lock (_lockerForRsa)
                    {
                        if (string.IsNullOrEmpty(rsaPublicKey)) return;
                        OnRsaProgress(0, 1);
                        int max = _appData.VmItemList.Count() * 3 + 2 + 1;
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
                        Debug.Assert(Rsa != null);
                        foreach (var vmProtocolServer in _appData.VmItemList)
                        {
                            DecryptPwdIfItIsEncrypted(vmProtocolServer.Server);
                            OnRsaProgress(++val, max);
                        }

                        // remove rsa keys from db
                        Rsa = null;
                        this._db.Set_RSA_SHA1("");
                        this._db.Set_RSA_PublicKey("");
                        this._db.Set_RSA_PrivateKeyPath("");
                        RaisePropertyChanged(nameof(RsaPublicKey));
                        RaisePropertyChanged(nameof(RsaPrivateKeyPath));

                        // update db
                        foreach (var vmProtocolServer in _appData.VmItemList)
                        {
                            this._db.UpdateServer(vmProtocolServer.Server);
                            OnRsaProgress(++val, max);
                        }

                        // del key
                        //File.Delete(ppkPath);

                        // del back up
                        File.Delete(_dbPath + ".back");

                        // done
                        OnRsaProgress(0, 0);
                    }
                });
            t.Start();
        }

        public void EncryptPwdIfItIsNotEncrypted(ProtocolServerBase server)
        {
            var rsa = Rsa;
            if (rsa != null)
            {
                if (server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase)))
                {
                    var s = (ProtocolServerWithAddrPortUserPwdBase)server;
                    if (rsa.DecodeOrNull(s.Password) == null)
                        s.Password = rsa.Encode(s.Password);
                }
                if (server is ProtocolServerSSH ssh
                    && !string.IsNullOrWhiteSpace(ssh.PrivateKey))
                {
                    if (rsa.DecodeOrNull(ssh.PrivateKey) == null)
                        ssh.PrivateKey = rsa.Encode(ssh.PrivateKey);
                }
                if (server is ProtocolServerRDP rdp
                    && !string.IsNullOrWhiteSpace(rdp.GatewayPassword))
                {
                    if (rsa.DecodeOrNull(rdp.GatewayPassword) == null)
                        rdp.GatewayPassword = rsa.Encode(rdp.GatewayPassword);
                }
            }
        }

        public void DecryptPwdIfItIsEncrypted(ProtocolServerBase server)
        {
            var rsa = Rsa;
            if (rsa != null)
            {
                if (server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase)))
                {
                    var s = (ProtocolServerWithAddrPortUserPwdBase)server;
                   if(rsa.DecodeOrNull(s.Password) == null)
                   {
                       return;
                   }
                    s.Password = rsa.DecodeOrNull(s.Password);
                }
                if (server is ProtocolServerSSH ssh
                && !string.IsNullOrWhiteSpace(ssh.PrivateKey))
                {
                    Debug.Assert(rsa.DecodeOrNull(ssh.PrivateKey) != null);
                    ssh.PrivateKey = rsa.DecodeOrNull(ssh.PrivateKey);
                }
                if (server is ProtocolServerRDP rdp
                && !string.IsNullOrWhiteSpace(rdp.GatewayPassword))
                {
                    Debug.Assert(rsa.DecodeOrNull(rdp.GatewayPassword) != null);
                    rdp.GatewayPassword = rsa.DecodeOrNull(rdp.GatewayPassword);
                }
            }
        }

        private void EncryptInfo(ProtocolServerBase server)
        {
            var rsa = Rsa;
            if (rsa == null) return;
            Debug.Assert(rsa.DecodeOrNull(server.DispName) == null);
            server.DispName = rsa.Encode(server.DispName);
            server.GroupName = rsa.Encode(server.GroupName);

            if (server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortBase)))
            {
                var p = (ProtocolServerWithAddrPortUserPwdBase)server;
                if (!string.IsNullOrEmpty(p.UserName))
                    p.UserName = rsa.Encode(p.UserName);
                if (!string.IsNullOrEmpty(p.Address))
                    p.Address = rsa.Encode(p.Address);
            }
        }

        public void DecryptInfo(ProtocolServerBase server)
        {
            var rsa = Rsa;
            if (rsa == null) return;
            Debug.Assert(rsa.DecodeOrNull(server.DispName) != null);
            server.DispName = rsa.DecodeOrNull(server.DispName);
            server.GroupName = rsa.DecodeOrNull(server.GroupName);

            if (server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortBase)))
            {
                var p = (ProtocolServerWithAddrPortUserPwdBase)server;
                if (!string.IsNullOrEmpty(p.UserName))
                    p.UserName = rsa.DecodeOrNull(p.UserName) ?? p.UserName;
                if (!string.IsNullOrEmpty(p.Address))
                    p.Address = rsa.DecodeOrNull(p.Address) ?? p.Address;
            }
        }

        public void DbAddServer(ProtocolServerBase org)
        {
            EncryptPwdIfItIsNotEncrypted(org);
            var tmp = (ProtocolServerBase)org.Clone();
            tmp.SetNotifyPropertyChangedEnabled(false);
            EncryptInfo(tmp);
            _db.AddServer(tmp);
        }
        public void DbUpdateServer(ProtocolServerBase org)
        {
            EncryptPwdIfItIsNotEncrypted(org);
            var tmp = (ProtocolServerBase)org.Clone();
            tmp.SetNotifyPropertyChangedEnabled(false);
            EncryptInfo(tmp);
            _db.UpdateServer(tmp);
        }

        public bool DbDeleteServer(int id)
        {
            return _db.DeleteServer(id);
        }

        public List<ProtocolServerBase> GetServers()
        {
            return _db.GetServers();
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
            DbPath = _ini.GetValue(nameof(DbPath).ToLower(), SectionName);
            StopAutoSave = false;
        }

        public override void Update(SystemConfigBase newConfig)
        {
            UpdateBase(this, newConfig, typeof(SystemConfigDataSecurity));
            Save();
        }
        #endregion

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
                if (_cmdGenRsaKey == null)
                {
                    _cmdGenRsaKey = new RelayCommand((o) =>
                    {
                        GenRsa();
                    });
                }
                return _cmdGenRsaKey;
            }
        }


        private RelayCommand _cmdClearRsaKey;
        public RelayCommand CmdClearRsaKey
        {
            get
            {
                if (_cmdClearRsaKey == null)
                {
                    _cmdClearRsaKey = new RelayCommand((o) =>
                    {
                        var res = CheckIfDbIsOk();
                        if (!res.Item1)
                        {
                            MessageBox.Show(res.Item2, SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                            if (MessageBoxResult.Yes == MessageBox.Show(
                                SystemConfig.Instance.Language.GetText("system_options_data_security_info_clear_rsa"),
                                SystemConfig.Instance.Language.GetText("messagebox_title_warning"), MessageBoxButton.YesNo, 
                                MessageBoxImage.Warning, MessageBoxResult.None))
                            {
                                if (File.Exists(DbPath))
                                    File.Delete(DbPath);
                                _appData.ServerListUpdate();
                                Load();
                            }
                        }
                        else
                            CleanRsa();
                    });
                }
                return _cmdClearRsaKey;
            }
        }


        private RelayCommand _cmdSelectDbPath;
        public RelayCommand CmdSelectDbPath
        {
            get
            {
                if (_cmdSelectDbPath == null)
                {
                    _cmdSelectDbPath = new RelayCommand((o) =>
                    {
                        var dlg = new OpenFileDialog();
                        dlg.Filter = "Sqlite Database|*.db";
                        dlg.CheckFileExists = false;
                        dlg.InitialDirectory = new FileInfo(DbPath).DirectoryName;
                        if (dlg.ShowDialog() == true)
                        {
                            var path = dlg.FileName;
                            var oldDbPath = DbPath;
                            try
                            {
                                if (IOPermissionHelper.HasWritePermissionOnFile(path))
                                {
                                    this._db.OpenConnection(DatabaseType.Sqlite, FreeSqlDb.GetConnectionStringSqlite(path));
                                    _appData.ServerListUpdate();
                                    DbPath = path;
                                }
                                else
                                    MessageBox.Show(SystemConfig.Instance.Language.GetText("system_options_data_security_error_can_not_open"), SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                            }
                            catch (Exception ee)
                            {
                                DbPath = oldDbPath;
                                SimpleLogHelper.Warning(ee);
                                MessageBox.Show(SystemConfig.Instance.Language.GetText("system_options_data_security_error_can_not_open"), SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                            }
                        }
                    });
                }
                return _cmdSelectDbPath;
            }
        }

        private RelayCommand _cmdDbMigrate;
        public RelayCommand CmdDbMigrate
        {
            get
            {
                if (_cmdDbMigrate == null)
                {
                    _cmdDbMigrate = new RelayCommand((o) =>
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
                                    this._db.CloseConnection();
                                    File.Move(oldDbPath, path);
                                    File.Delete(oldDbPath);
                                    this._db.OpenConnection(DatabaseType.Sqlite, FreeSqlDb.GetConnectionStringSqlite(path));
                                    // Migrate do not need reload data
                                    // _appData.ServerListUpdate();
                                    DbPath = path;
                                }
                                else
                                    MessageBox.Show(SystemConfig.Instance.Language.GetText("system_options_data_security_error_can_not_open"), SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                            }
                            catch (Exception ee)
                            {
                                SimpleLogHelper.Debug(ee);
                                DbPath = oldDbPath;
                                MessageBox.Show(SystemConfig.Instance.Language.GetText("system_options_data_security_error_can_not_open"), SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                            }
                        }
                    });
                }
                return _cmdDbMigrate;
            }
        }
        #endregion
    }
}
