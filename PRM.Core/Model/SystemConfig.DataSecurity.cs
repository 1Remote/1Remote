using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.IO;
using com.github.xiangyuecn.rsacsharp;
using Microsoft.Win32;
using Newtonsoft.Json;
using PRM.Core.DB;
using PRM.Core.Protocol;
using PRM.Core.Protocol.Putty.SSH;
using PRM.Core.Protocol.Putty.Telnet;
using PRM.Core.Protocol.RDP;
using Shawn.Ulits;
using SQLite;

namespace PRM.Core.Model
{
    public sealed class SystemConfigDataSecurity : SystemConfigBase
    {
        public SystemConfigDataSecurity(Ini ini) : base(ini)
        {
            Load();

            // restore from back. (in these case .back will existed: data encrypt/decrypt processing throw exception)
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
                    Server.Init();
                    return new Tuple<bool, string>(true, "");
                }
                else
                {
                    return new Tuple<bool, string>(false, "TXT:db permission denied:" + " " + path);
                }
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
                SimpleLogHelper.Error(e.StackTrace);
                return new Tuple<bool, string>(false, e.Message);
            }
        }

        /// <summary>
        /// Check if db is(can) decrypted by the private key
        /// </summary>
        /// <returns>
        /// Tuple(is decrypted, error info)
        /// </returns>
        public Tuple<bool, string> CheckIfDbIsOk(string privateKeyPath = "")
        {
            var c1 = CheckIfDbIsWritable();
            if (!c1.Item1)
                return c1;

            var _privateKeyPath = DB.Config.RSA_PrivateKeyPath;
            if (!string.IsNullOrEmpty(privateKeyPath))
                _privateKeyPath = privateKeyPath;

            // NO RSA
            if (string.IsNullOrEmpty(DB.Config.RSA_PublicKey)
                && string.IsNullOrEmpty(_privateKeyPath)
                && string.IsNullOrEmpty(DB.Config.RSA_SHA1))
                return new Tuple<bool, string>(true, "");

            if (!File.Exists(_privateKeyPath))
                return new Tuple<bool, string>(false, SystemConfig.Instance.Language.GetText("system_options_data_security_error_rsa_private_key_not_found"));

            RSA rsaPk = null;
            RSA rsaPpk = null;

            try
            {
                rsaPpk = new RSA(File.ReadAllText(_privateKeyPath), true);
            }
            catch (Exception)
            {
                return new Tuple<bool, string>(false, SystemConfig.Instance.Language.GetText("system_options_data_security_error_rsa_private_key_not_match"));
            }

            // make sure public key is PEM format key
            try
            {
                rsaPk = new RSA(DB.Config.RSA_PublicKey, true);
            }
            catch (Exception)
            {
                // try to fix public key
                if (rsaPpk.Verify("SHA1", DB.Config.RSA_SHA1, SystemConfig.AppName))
                {
                    DB.Config.RSA_PublicKey = rsaPpk.ToPEM_PKCS1(true);
                    rsaPk = new RSA(File.ReadAllText(_privateKeyPath), true);
                }
            }
            // RSA private key is match public key?
            try
            {
                rsaPpk = new RSA(File.ReadAllText(_privateKeyPath), true);
                var sha1 = rsaPpk.Sign("SHA1", SystemConfig.AppName);
                if (!rsaPk.Verify("SHA1", sha1, SystemConfig.AppName))
                {
                    throw new Exception("RSA key is not match!");
                }
                DB.Config.RSA_SHA1 = sha1;
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
            set
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

        public string RsaPublicKey => DB.Config.RSA_PublicKey;

        public string RsaPrivateKeyPath
        {
            get => DB.Config.RSA_PrivateKeyPath;
        }

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
                    if (string.IsNullOrEmpty(DB.Config.RSA_PublicKey))
                        return null;
                    _rsa = new RSA(File.ReadAllText(DB.Config.RSA_PrivateKeyPath), true);
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
                MessageBox.Show(res.Item2, SystemConfig.Instance.Language.GetText("messagebox_title_error"));
                return;
            }

            if (!string.IsNullOrEmpty(DB.Config.RSA_PublicKey))
                return;

            if (string.IsNullOrEmpty(DB.Config.RSA_PublicKey))
            {
                var t = new Task(() =>
                {
                    lock (_lockerForRsa)
                    {
                        if (string.IsNullOrEmpty(DB.Config.RSA_PublicKey))
                        {
                            var protocolServerBases = GlobalData.Instance.ServerList;
                            int max = protocolServerBases.Count() * 3 + 2;
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
                                DB.Config.RSA_SHA1 = rsa.Sign("SHA1", SystemConfig.AppName);
                                DB.Config.RSA_PublicKey = rsa.ToPEM_PKCS1(true);
                                DB.Config.RSA_PrivateKeyPath = dlg.FileName;
                                RaisePropertyChanged(nameof(RsaPublicKey));
                                RaisePropertyChanged(nameof(RsaPrivateKeyPath));

                                // encrypt old data
                                foreach (var psb in protocolServerBases)
                                {
                                    EncryptPwd(psb);
                                    OnRsaProgress(++val, max);
                                    Server.AddOrUpdate(psb);
                                    OnRsaProgress(++val, max);
                                }

                                // del back up
                                File.Delete(_dbPath + ".back");

                                // done
                                OnRsaProgress(0, 0);
                            }
                        }
                    }
                });
                t.Start();
            }
        }
        public void CleanRsa()
        {
            // validate rsa key
            var res = SystemConfig.Instance.DataSecurity.CheckIfDbIsOk();
            if (!res.Item1)
            {
                MessageBox.Show(res.Item2, SystemConfig.Instance.Language.GetText("messagebox_title_error"));
                return;
            }

            if (!string.IsNullOrEmpty(DB.Config.RSA_PublicKey))
            {
                var t = new Task(() =>
                {
                    lock (_lockerForRsa)
                    {
                        if (!string.IsNullOrEmpty(DB.Config.RSA_PublicKey))
                        {
                            OnRsaProgress(0, 1);
                            var protocolServerBases = GlobalData.Instance.ServerList;
                            int max = protocolServerBases.Count() * 3 + 2 + 1;
                            int val = 1;
                            OnRsaProgress(val, max);

                            // database back up
                            Debug.Assert(File.Exists(DbPath));
                            File.Copy(_dbPath, _dbPath + ".back", true);
                            OnRsaProgress(++val, max);

                            // keep pld rsa
                            //var ppkPath = DB.Config.RSA_PrivateKeyPath;
                            //var rsa = new RSA(File.ReadAllText(DB.Config.RSA_PrivateKeyPath), true);

                            // decrypt pwd
                            Debug.Assert(Rsa != null);
                            foreach (var psb in protocolServerBases)
                            {
                                DecryptPwd(psb);
                                OnRsaProgress(++val, max);
                            }

                            // remove rsa keys from db
                            Rsa = null;
                            DB.Config.RSA_SHA1 = "";
                            DB.Config.RSA_PublicKey = "";
                            DB.Config.RSA_PrivateKeyPath = "";
                            RaisePropertyChanged(nameof(RsaPublicKey));
                            RaisePropertyChanged(nameof(RsaPrivateKeyPath));
                            
                            // update
                            foreach (var psb in protocolServerBases)
                            {
                                Server.AddOrUpdate(psb);
                                OnRsaProgress(++val, max);
                            }



                            // del key
                            //File.Delete(ppkPath);

                            // del back up
                            File.Delete(_dbPath + ".back");

                            // done
                            OnRsaProgress(0, 0);
                        }
                    }
                });
                t.Start();
            }
        }

        public void EncryptPwd(ProtocolServerBase server)
        {
            var rsa = Rsa;
            if (rsa != null)
            {
                if (server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase)))
                {
                    var s = (ProtocolServerWithAddrPortUserPwdBase) server;
                    Debug.Assert(rsa.DecodeOrNull(s.Password) == null);
                    s.Password = rsa.Encode(s.Password);
                }
                if (server.GetType().IsSubclassOf(typeof(ProtocolServerSSH)))
                {
                    var s = (ProtocolServerSSH) server;
                    s.PrivateKey = rsa.Encode(s.PrivateKey);
                }
            }
        }

        public void DecryptPwd(ProtocolServerBase server)
        {
            var rsa = Rsa;
            if (rsa != null)
            {
                if (server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase)))
                {
                    var s = (ProtocolServerWithAddrPortUserPwdBase) server;
                    Debug.Assert(rsa.DecodeOrNull(s.Password) != null);
                    s.Password = rsa.DecodeOrNull(s.Password);
                }
                if (server.GetType().IsSubclassOf(typeof(ProtocolServerSSH)))
                {
                    var s = (ProtocolServerSSH) server;
                    Debug.Assert(rsa.DecodeOrNull(s.PrivateKey) != null);
                    s.PrivateKey = rsa.DecodeOrNull(s.PrivateKey);
                }
            }
        }

        public void EncryptInfo(ProtocolServerBase server)
        {
            var rsa = Rsa;
            if (rsa != null)
            {
                Debug.Assert(rsa.DecodeOrNull(server.DispName) == null);
                server.DispName = rsa.Encode(server.DispName);
                server.GroupName = rsa.Encode(server.GroupName);
                switch (server)
                {
                    case ProtocolServerRDP _:
                    case ProtocolServerSSH _:
                    case ProtocolServerWithAddrPortUserPwdBase _:
                        var p = (ProtocolServerWithAddrPortUserPwdBase) server;
                        if (!string.IsNullOrEmpty(p.UserName))
                            p.UserName = rsa.Encode(p.UserName);
                        if (!string.IsNullOrEmpty(p.Address))
                            p.Address = rsa.Encode(p.Address);
                        break;
                    default:
                        break;
                }
            }
        }

        public void DecryptInfo(ProtocolServerBase server)
        {
            var rsa = Rsa;
            if (rsa != null)
            {
                Debug.Assert(rsa.DecodeOrNull(server.DispName) != null);
                server.DispName = rsa.DecodeOrNull(server.DispName);
                server.GroupName = rsa.DecodeOrNull(server.GroupName);
                switch (server)
                {
                    case ProtocolServerRDP _:
                    case ProtocolServerSSH _:
                    case ProtocolServerWithAddrPortUserPwdBase _:
                        {
                            var p = (ProtocolServerWithAddrPortUserPwdBase) server;
                            if (!string.IsNullOrEmpty(p.UserName))
                                p.UserName = rsa.DecodeOrNull(p.UserName);
                            if (!string.IsNullOrEmpty(p.Address))
                                p.Address = rsa.DecodeOrNull(p.Address);
                            break;
                        }
                    case ProtocolServerWithAddrPortBase _:
                        {
                            var p = (ProtocolServerWithAddrPortBase) server;
                            if (!string.IsNullOrEmpty(p.Address))
                                p.Address = rsa.DecodeOrNull(p.Address);
                            break;
                        }
                    default:
                        break;
                }
            }
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
                                    using (var db = new SQLiteConnection(dlg.FileName))
                                    {
                                        db.CreateTable<Server>();
                                    }
                                    DbPath = dlg.FileName;
                                    GlobalData.Instance.ServerListUpdate();
                                }
                                else
                                    MessageBox.Show(SystemConfig.Instance.Language.GetText("system_options_data_security_error_can_not_open"), SystemConfig.Instance.Language.GetText("messagebox_title_error"));
                            }
                            catch (Exception ee)
                            {
                                DbPath = oldDbPath;
                                MessageBox.Show(SystemConfig.Instance.Language.GetText("system_options_data_security_error_can_not_open"), SystemConfig.Instance.Language.GetText("messagebox_title_error"));
                            }
                        }
                    });
                }
                return _cmdSelectDbPath;
            }
        }

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
                                MessageBox.Show(res.Item2, SystemConfig.Instance.Language.GetText("messagebox_title_error"));
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
                            MessageBox.Show(res.Item2, SystemConfig.Instance.Language.GetText("messagebox_title_error"));
                            if (MessageBox.Show(SystemConfig.Instance.Language.GetText("system_options_data_security_info_clear_rebuild_database"), SystemConfig.Instance.Language.GetText("messagebox_title_warning"), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                            {
                                if (File.Exists(DbPath))
                                    File.Delete(DbPath);
                                GlobalData.Instance.ServerListUpdate();
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

        private RelayCommand _cmdExportToJson;
        public RelayCommand CmdExportToJson
        {
            get
            {
                if (_cmdExportToJson == null)
                {
                    _cmdExportToJson = new RelayCommand((o) =>
                    {
                        var res = CheckIfDbIsOk();
                        if (!res.Item1)
                        {
                            MessageBox.Show(res.Item2, SystemConfig.Instance.Language.GetText("messagebox_title_error"));
                            return;
                        }
                        var dlg = new SaveFileDialog
                        {
                            Filter = "PRM json array|*.prma",
                            Title = SystemConfig.Instance.Language.GetText("system_options_data_security_export_dialog_title"),
                            FileName = DateTime.Now.ToString("yyyyMMddhhmmss") + ".prma"
                        };
                        if (dlg.ShowDialog() == true)
                        {
                            var list = new List<ProtocolServerBase>();
                            foreach (var protocolServerBase in GlobalData.Instance.ServerList)
                            {
                                var serverBase = (ProtocolServerBase) protocolServerBase.Clone();
                                if (serverBase is ProtocolServerRDP
                                    || serverBase is ProtocolServerSSH)
                                {
                                    var obj = (ProtocolServerWithAddrPortUserPwdBase) serverBase;
                                    if (Rsa != null)
                                        obj.Password = Rsa.DecodeOrNull(obj.Password) ?? "";
                                }
                                list.Add(serverBase);
                            }
                            File.WriteAllText(dlg.FileName, JsonConvert.SerializeObject(list, Formatting.Indented), Encoding.UTF8);
                        }
                    });
                }
                return _cmdExportToJson;
            }
        }

        private RelayCommand _cmdExportToCsv;
        public RelayCommand CmdExportToCsv
        {
            get
            {
                if (_cmdExportToCsv == null)
                {
                    _cmdExportToCsv = new RelayCommand((o) =>
                    {
                        var res = CheckIfDbIsOk();
                        if (!res.Item1)
                        {
                            MessageBox.Show(res.Item2, SystemConfig.Instance.Language.GetText("messagebox_title_error"));
                            return;
                        }
                        var dlg = new SaveFileDialog
                        {
                            Filter = "PRM csv data|*.csv",
                            Title = SystemConfig.Instance.Language.GetText("system_options_data_security_export_dialog_title"),
                            FileName = DateTime.Now.ToString("yyyyMMddhhmmss") + ".csv"
                        };
                        if (dlg.ShowDialog() == true)
                        {
                            var sb = new StringBuilder();
                            sb.AppendLine("name;protocol;panel;hostname;port;username;password");
                            foreach (var protocolServerBase in GlobalData.Instance.ServerList)
                            {
                                var protocol = "";
                                var name = "";
                                var group ="";
                                var user = "";
                                var pwd = "";
                                var address = "";
                                string port = "";

                                var serverBase = (ProtocolServerBase) protocolServerBase.Clone();
                                name = serverBase.DispName;
                                group = serverBase.GroupName;
                                protocol = serverBase.Protocol;
                                if (serverBase.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase))
                                    || serverBase is ProtocolServerRDP
                                    || serverBase is ProtocolServerSSH)
                                {
                                    var obj = (ProtocolServerWithAddrPortUserPwdBase) serverBase;
                                    pwd = obj.Password;
                                    if (Rsa != null)
                                        pwd = Rsa.DecodeOrNull(pwd) ?? obj.Password;
                                    user = obj.UserName;
                                    address = obj.Address;
                                    port = obj.Port;
                                }
                                else if (serverBase.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortBase)))
                                {
                                    var obj = (ProtocolServerWithAddrPortBase) serverBase;
                                    address = obj.Address;
                                    port = obj.Port;
                                }

                                const string mark = "%THIS_IS_A_SENUCOLON%";
                                protocol = protocol.Replace(";", mark);
                                name = name.Replace(";", mark);
                                group = group.Replace(";", mark);
                                user = user.Replace(";", mark);
                                pwd = pwd.Replace(";", mark);
                                address = address.Replace(";", mark);
                                port = port.Replace(";", mark);
                                sb.AppendLine($"{name};{protocol};{group};{address};{port};{user};{pwd}");
                            }
                            File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                        }
                    });
                }
                return _cmdExportToCsv;
            }
        }

        private RelayCommand _cmdImportFromJson;
        public RelayCommand CmdImportFromJson
        {
            get
            {
                if (_cmdImportFromJson == null)
                {
                    _cmdImportFromJson = new RelayCommand((o) =>
                    {
                        var res = CheckIfDbIsOk();
                        if (!res.Item1)
                        {
                            MessageBox.Show(res.Item2, SystemConfig.Instance.Language.GetText("messagebox_title_error"));
                            return;
                        }
                        var dlg = new OpenFileDialog()
                        {
                            Filter = "PRM json array|*.prma",
                            Title = SystemConfig.Instance.Language.GetText("system_options_data_security_import_dialog_title"),
                            FileName = DateTime.Now.ToString("yyyyMMddhhmmss") + ".prma"
                        };
                        if (dlg.ShowDialog() == true)
                        {
                            try
                            {
                                var list = new List<ProtocolServerBase>();
                                var jobj = JsonConvert.DeserializeObject<List<object>>(File.ReadAllText(dlg.FileName, Encoding.UTF8));
                                foreach (var json in jobj)
                                {
                                    var server = ServerCreateHelper.CreateFromJsonString(json.ToString());
                                    if (server != null)
                                    {
                                        server.Id = 0;
                                        list.Add(server);
                                    }
                                }
                                if (list?.Count > 0)
                                {
                                    foreach (var serverBase in list)
                                    {
                                        if (serverBase is ProtocolServerRDP
                                            || serverBase is ProtocolServerSSH
                                            || serverBase.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortBase)))
                                        {
                                            var pwd = (ProtocolServerWithAddrPortUserPwdBase) serverBase;
                                            if (Rsa != null)
                                                pwd.Password = Rsa.Encode(pwd.Password);
                                        }
                                        Server.AddOrUpdate(serverBase, true);
                                    }
                                }
                                GlobalData.Instance.ServerListUpdate();
                                MessageBox.Show(SystemConfig.Instance.Language.GetText("system_options_data_security_import_done"));
                            }
                            catch (Exception e)
                            {
                                MessageBox.Show(SystemConfig.Instance.Language.GetText("system_options_data_security_import_error"), SystemConfig.Instance.Language.GetText("messagebox_title_error"));
                            }
                        }
                    });
                }
                return _cmdImportFromJson;
            }
        }

        private RelayCommand _cmdImportFromCsv;
        public RelayCommand CmdImportFromCsv
        {
            get
            {
                if (_cmdImportFromCsv == null)
                {
                    _cmdImportFromCsv = new RelayCommand((o) =>
                    {
                        var res = CheckIfDbIsOk();
                        if (!res.Item1)
                        {
                            MessageBox.Show(res.Item2, SystemConfig.Instance.Language.GetText("messagebox_title_error"));
                            return;
                        }
                        var dlg = new OpenFileDialog()
                        {
                            Filter = "csv|*.csv",
                            Title = SystemConfig.Instance.Language.GetText("system_options_data_security_import_dialog_title"),
                        };
                        if (dlg.ShowDialog() == true)
                        {
                            try
                            {
                                var list = new List<ProtocolServerBase>();
                                using (var sr = new StreamReader(new FileStream(dlg.FileName, FileMode.Open)))
                                {
                                    // title
                                    var title = sr.ReadLine().ToLower().Split(';').ToList();
                                    int protocolIndex = title.IndexOf("protocol");
                                    int nameIndex = title.IndexOf("name");
                                    int groupIndex = title.IndexOf("panel");
                                    int userIndex = title.IndexOf("username");
                                    int pwdIndex = title.IndexOf("password");
                                    int addressIndex = title.IndexOf("hostname");
                                    int portIndex = title.IndexOf("port");
                                    if (protocolIndex == 0)
                                        throw new ArgumentException("can't find protocol field");

                                    var r = new Random();
                                    // body
                                    var line = sr.ReadLine();
                                    while (!string.IsNullOrEmpty(line))
                                    {
                                        var arr = line.Split(';');
                                        if (arr.Length >= 7)
                                        {
                                            ProtocolServerBase server = null;
                                            var protocol = arr[protocolIndex].ToLower();
                                            var name = "";
                                            var group ="";
                                            var user = "";
                                            var pwd = "";
                                            var address = "";
                                            int port = 22;
                                            if (nameIndex >= 0)
                                                name = arr[nameIndex];
                                            if (groupIndex >= 0)
                                                group = arr[groupIndex];
                                            if (userIndex >= 0)
                                                user = arr[userIndex];
                                            if (pwdIndex >= 0)
                                                pwd = arr[pwdIndex];
                                            if (addressIndex >= 0)
                                                address = arr[addressIndex];
                                            if (portIndex >= 0)
                                                port = int.Parse(arr[portIndex]);


                                            const string mark = "%THIS_IS_A_SENUCOLON%";
                                            protocol = protocol.Replace(mark, ";");
                                            name = name.Replace(mark, ";");
                                            group = group.Replace(mark, ";");
                                            user = user.Replace(mark, ";");
                                            pwd = pwd.Replace(mark, ";");
                                            address = address.Replace(mark, ";");

                                            switch (protocol)
                                            {
                                                case "rdp":
                                                    server = new ProtocolServerRDP()
                                                    {
                                                        DispName = name,
                                                        GroupName = group,
                                                        Address = address,
                                                        UserName = user,
                                                        Password = pwd,
                                                        Port = port.ToString(),
                                                    };
                                                    break;
                                                case "ssh1":
                                                    server = new ProtocolServerSSH()
                                                    {
                                                        DispName = name,
                                                        GroupName = group,
                                                        Address = address,
                                                        UserName = user,
                                                        Password = pwd,
                                                        Port = port.ToString(),
                                                        SshVersion = ProtocolServerSSH.ESshVersion.V1
                                                    };
                                                    break;
                                                case "ssh2":
                                                    server = new ProtocolServerSSH()
                                                    {
                                                        DispName = name,
                                                        GroupName = group,
                                                        Address = address,
                                                        UserName = user,
                                                        Password = pwd,
                                                        Port = port.ToString(),
                                                        SshVersion = ProtocolServerSSH.ESshVersion.V2
                                                    };
                                                    break;
                                                case "vnc":
                                                    // TODO add vnc
                                                    break;
                                                case "telnet":
                                                    server = new ProtocolServerTelnet()
                                                    {
                                                        DispName = name,
                                                        GroupName = group,
                                                        Address = address,
                                                        Port = port.ToString(),
                                                    };
                                                    break;
                                            }

                                            if (server != null)
                                            {
                                                server.IconImg = ServerIcons.Instance.Icons[r.Next(0, ServerIcons.Instance.Icons.Count)];
                                                list.Add(server);
                                            }
                                        }
                                        line = sr.ReadLine();
                                    }
                                }
                                if (list?.Count > 0)
                                {
                                    foreach (var serverBase in list)
                                    {
                                        if (serverBase is ProtocolServerRDP
                                            || serverBase is ProtocolServerSSH)
                                        {
                                            var pwd = (ProtocolServerWithAddrPortUserPwdBase) serverBase;
                                            if (Rsa != null)
                                                pwd.Password = Rsa.Encode(pwd.Password);
                                        }
                                        Server.AddOrUpdate(serverBase, true);
                                    }
                                }
                                GlobalData.Instance.ServerListUpdate();
                                MessageBox.Show(SystemConfig.Instance.Language.GetText("system_options_data_security_import_done"));
                            }
                            catch (Exception e)
                            {
                                MessageBox.Show(SystemConfig.Instance.Language.GetText("system_options_data_security_import_error"));
                            }
                        }
                    });
                }
                return _cmdImportFromCsv;
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
                        var dlg = new SaveFileDialog();
                        dlg.Filter = "Sqlite Database|*.db";
                        dlg.CheckFileExists = false;
                        dlg.InitialDirectory = new FileInfo(DbPath).DirectoryName;
                        dlg.FileName = new FileInfo(DbPath).Name;
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
                                    File.Move(oldDbPath, path);
                                    File.Delete(oldDbPath);
                                    DbPath = path;
                                    GlobalData.Instance.ServerListUpdate();
                                }
                                else
                                    MessageBox.Show(SystemConfig.Instance.Language.GetText("system_options_data_security_error_can_not_open"), SystemConfig.Instance.Language.GetText("messagebox_title_error"));
                            }
                            catch (Exception ee)
                            {
                                DbPath = oldDbPath;
                                MessageBox.Show(SystemConfig.Instance.Language.GetText("system_options_data_security_error_can_not_open"), SystemConfig.Instance.Language.GetText("messagebox_title_error"));
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
