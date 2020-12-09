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
using PRM.Core.Protocol.VNC;
using Shawn.Utils;
using SQLite;
using FreeSql.DataAnnotations;
namespace PRM.Core.Model
{
    public class Blog
    {
        [FreeSql.DataAnnotations.Column(IsIdentity = true, IsPrimary = true)]
        public int BlogId { get; set; }
        public string Url { get; set; }
        public int Rating { get; set; }
    }
    public sealed class SystemConfigDataSecurity : SystemConfigBase
    {
        public SystemConfigDataSecurity(Ini ini) : base(ini)
        {

            //IFreeSql fsql = new FreeSql.FreeSqlBuilder()
            //    .UseConnectionString(FreeSql.DataType.Sqlite, "Data Source=db1.db", typeof(FreeSql.Sqlite.SqliteProvider<>))
            //    .UseAutoSyncStructure(true) //自动同步实体结构到数据库
            //    .Build(); //请务必定义成 Singleton 单例模式

            //var blog = new Blog { Url = "http://sample.com" };
            //blog.BlogId = (int)fsql.Insert<Blog>()
            //    .AppendData(blog)
            //    .ExecuteIdentity();

            //var blogs = fsql.Select<Blog>()
            //    .Where(b => b.Rating > 3)
            //    .OrderBy(b => b.Url)
            //    .Skip(100)
            //    .Limit(10) //第100行-110行的记录
            //    .ToList();

            //fsql.Dispose();




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
                    return new Tuple<bool, string>(false, SystemConfig.Instance.Language.GetText("string_permission_denied") + $": {path}");
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
                MessageBox.Show(res.Item2, SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
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
                            var protocolServerBases = GlobalData.Instance.VmItemList;
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
                                        SimpleLogHelper.Debug(e, e.StackTrace);
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
                                    EncryptPwd(psb.Server);
                                    OnRsaProgress(++val, max);
                                    Server.AddOrUpdate(psb.Server);
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
                MessageBox.Show(res.Item2, SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
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
                            var list = GlobalData.Instance.VmItemList;
                            int max = list.Count() * 3 + 2 + 1;
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
                            foreach (var vs in list)
                            {
                                DecryptPwd(vs.Server);
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
                            foreach (var vs in list)
                            {
                                Server.AddOrUpdate(vs.Server);
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
                    var s = (ProtocolServerWithAddrPortUserPwdBase)server;
                    Debug.Assert(rsa.DecodeOrNull(s.Password) == null);
                    s.Password = rsa.Encode(s.Password);
                }
                if (server is ProtocolServerSSH ssh
                    && !string.IsNullOrWhiteSpace(ssh.PrivateKey))
                {
                    ssh.PrivateKey = rsa.Encode(ssh.PrivateKey);
                }
                if (server is ProtocolServerRDP rdp
                    && !string.IsNullOrWhiteSpace(rdp.GatewayPassword))
                {
                    rdp.GatewayPassword = rsa.Encode(rdp.GatewayPassword);
                }
            }
        }


        ///// <summary>
        ///// return a copied object of server which is decrypted Pwd
        ///// </summary>
        ///// <typeparam name="T"></typeparam>
        ///// <param name="server"></param>
        ///// <returns></returns>
        //public T DecryptPwd<T>(T server) where T : ProtocolServerWithAddrPortUserPwdBase
        //{
        //    var ret = (T)server.Clone();
        //    var rsa = Rsa;
        //    if (rsa != null)
        //    {
        //        if (ret.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase)))
        //        {
        //            var s = (ProtocolServerWithAddrPortUserPwdBase)ret;
        //            Debug.Assert(rsa.DecodeOrNull(s.Password) != null);
        //            s.Password = rsa.DecodeOrNull(s.Password);
        //        }
        //        if (ret is ProtocolServerSSH ssh
        //            && !string.IsNullOrWhiteSpace(ssh.PrivateKey))
        //        {
        //            Debug.Assert(rsa.DecodeOrNull(ssh.PrivateKey) != null);
        //            ssh.PrivateKey = rsa.DecodeOrNull(ssh.PrivateKey);
        //        }
        //        if (ret is ProtocolServerRDP rdp
        //            && !string.IsNullOrWhiteSpace(rdp.GatewayPassword))
        //        {
        //            Debug.Assert(rsa.DecodeOrNull(rdp.GatewayPassword) != null);
        //            rdp.GatewayPassword = rsa.DecodeOrNull(rdp.GatewayPassword);
        //        }
        //    }
        //    return ret;
        //}

        public void DecryptPwd(ProtocolServerBase server)
        {
            var rsa = Rsa;
            if (rsa != null)
            {
                if (server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase)))
                {
                    var s = (ProtocolServerWithAddrPortUserPwdBase)server;
                    Debug.Assert(rsa.DecodeOrNull(s.Password) != null);
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

        public void EncryptInfo(ProtocolServerBase server)
        {
            var rsa = Rsa;
            if (rsa != null)
            {
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
        }

        public void DecryptInfo(ProtocolServerBase server)
        {
            var rsa = Rsa;
            if (rsa != null)
            {
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
                                    MessageBox.Show(SystemConfig.Instance.Language.GetText("system_options_data_security_error_can_not_open"), SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                            }
                            catch (Exception ee)
                            {
                                DbPath = oldDbPath;
                                SimpleLogHelper.Warning(ee);
                                MessageBox.Show(SystemConfig.Instance.Language.GetText("system_options_data_security_error_can_not_open"), SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
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
                                MessageBox.Show(res.Item2, SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
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
                            MessageBox.Show(res.Item2, SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                            if (MessageBox.Show(SystemConfig.Instance.Language.GetText("system_options_data_security_info_clear_rebuild_database"), SystemConfig.Instance.Language.GetText("messagebox_title_warning"), MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly) == MessageBoxResult.Yes)
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
                            MessageBox.Show(res.Item2, SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
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
                            foreach (var vs in GlobalData.Instance.VmItemList)
                            {
                                var serverBase = (ProtocolServerBase)vs.Server.Clone();
                                DecryptPwd(serverBase);
                                list.Add(serverBase);
                            }
                            File.WriteAllText(dlg.FileName, JsonConvert.SerializeObject(list, Formatting.Indented), Encoding.UTF8);
                        }
                    });
                }
                return _cmdExportToJson;
            }
        }

        //private RelayCommand _cmdExportToCsv;
        //public RelayCommand CmdExportToCsv
        //{
        //    get
        //    {
        //        if (_cmdExportToCsv == null)
        //        {
        //            _cmdExportToCsv = new RelayCommand((o) =>
        //            {
        //                var res = CheckIfDbIsOk();
        //                if (!res.Item1)
        //                {
        //                    MessageBox.Show(res.Item2, SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
        //                    return;
        //                }
        //                var dlg = new SaveFileDialog
        //                {
        //                    Filter = "PRM csv data|*.csv",
        //                    Title = SystemConfig.Instance.Language.GetText("system_options_data_security_export_dialog_title"),
        //                    FileName = DateTime.Now.ToString("yyyyMMddhhmmss") + ".csv"
        //                };
        //                if (dlg.ShowDialog() == true)
        //                {
        //                    var sb = new StringBuilder();
        //                    sb.AppendLine("name;protocol;panel;hostname;port;username;password");
        //                    foreach (var vs in GlobalData.Instance.VmItemList)
        //                    {
        //                        var protocol = "";
        //                        var name = "";
        //                        var group = "";
        //                        var user = "";
        //                        var pwd = "";
        //                        var address = "";
        //                        string port = "";

        //                        var serverBase = (ProtocolServerBase)vs.Server.Clone();
        //                        name = serverBase.DispName;
        //                        group = serverBase.GroupName;
        //                        protocol = serverBase.Protocol;

        //                        DecryptPwd(serverBase);


        //                        // todo ADD RD GATEWAY
        //                        if (serverBase.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase)))
        //                        {
        //                            var obj = (ProtocolServerWithAddrPortUserPwdBase)serverBase;
        //                            user = obj.UserName;
        //                            address = obj.Address;
        //                            port = obj.Port;
        //                        }
        //                        else if (serverBase.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortBase)))
        //                        {
        //                            var obj = (ProtocolServerWithAddrPortBase)serverBase;
        //                            address = obj.Address;
        //                            port = obj.Port;
        //                        }

        //                        const string mark = "%THIS_IS_A_SENUCOLON%";
        //                        protocol = protocol.Replace(";", mark);
        //                        name = name.Replace(";", mark);
        //                        group = group.Replace(";", mark);
        //                        user = user.Replace(";", mark);
        //                        pwd = pwd.Replace(";", mark);
        //                        address = address.Replace(";", mark);
        //                        port = port.Replace(";", mark);
        //                        sb.AppendLine($"{name};{protocol};{group};{address};{port};{user};{pwd}");
        //                    }
        //                    File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
        //                }
        //            });
        //        }
        //        return _cmdExportToCsv;
        //    }
        //}

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
                            MessageBox.Show(res.Item2, SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
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
                                    var server = ItemCreateHelper.CreateFromJsonString(json.ToString());
                                    if (server != null)
                                    {
                                        server.Id = 0;
                                        list.Add(server);
                                        EncryptPwd(server);
                                        Server.AddOrUpdate(server, true);
                                    }
                                }
                                GlobalData.Instance.ServerListUpdate();
                                MessageBox.Show(SystemConfig.Instance.Language.GetText("system_options_data_security_import_done").Replace("{0}", list.Count.ToString()), SystemConfig.Instance.Language.GetText("messagebox_title_info"), MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                            }
                            catch (Exception e)
                            {
                                SimpleLogHelper.Debug(e, e.StackTrace);
                                MessageBox.Show(SystemConfig.Instance.Language.GetText("system_options_data_security_import_error"), SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
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
                            MessageBox.Show(res.Item2, SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
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
                                    var firstLine = sr.ReadLine();
                                    if(string.IsNullOrWhiteSpace(firstLine))
                                        return;

                                    // title
                                    var title = firstLine.ToLower().Split(';').ToList();

                                    //int RdpVersion = title.IndexOf("RdpVersion");

                                    //int vncCompression = title.IndexOf("VNCCompression");
                                    //int vncEncoding = title.IndexOf("VNCEncoding");
                                    //int vncAuthMode = title.IndexOf("VNCAuthMode");
                                    //int vncProxyType = title.IndexOf("VNCProxyType");
                                    //int vncProxyIP = title.IndexOf("VNCProxyIP");
                                    //int vncProxyPort = title.IndexOf("VNCProxyPort");
                                    //int vncProxyUsername = title.IndexOf("VNCProxyUsername");
                                    //int vncProxyPassword = title.IndexOf("VNCProxyPassword");
                                    //int vncColors = title.IndexOf("VNCColors");
                                    //int vncSmartSizeMode = title.IndexOf("VNCSmartSizeMode");
                                    //int vncViewOnly = title.IndexOf("VNCViewOnly");


                                    var r = new Random();
                                    // body
                                    var line = sr.ReadLine();
                                    while (!string.IsNullOrEmpty(line))
                                    {
                                        var arr = line.Split(';');
                                        if (arr.Length >= 7)
                                        {
                                            string getValue(string fieldName)
                                            {
                                                var i = title.IndexOf(fieldName.ToLower());
                                                if (i >= 0)
                                                {
                                                    var val = arr[i];
                                                    //val = val.Replace("%THIS_IS_A_SENUCOLON%", ";");
                                                    return val;
                                                }
                                                return "";
                                            }
                                            ProtocolServerBase server = null;
                                            var protocol = getValue("protocol").ToLower();
                                            var name = getValue("name");
                                            var group = getValue("panel");
                                            var user = getValue("username");
                                            var pwd = getValue("password");
                                            var address = getValue("hostname");
                                            int port = 22;
                                            if(int.TryParse(getValue("port"), out var new_port))
                                            {
                                                port = new_port;
                                            }

                                            // todo ADD RD GATEWAY
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
                                                        RdpWindowResizeMode = ERdpWindowResizeMode.AutoResize, // string.Equals(getValue("AutomaticResize"), "TRUE", StringComparison.CurrentCultureIgnoreCase) ? ERdpWindowResizeMode.AutoResize : ERdpWindowResizeMode.Fixed,
                                                        IsConnWithFullScreen = string.Equals(getValue("Resolution"), "Fullscreen", StringComparison.CurrentCultureIgnoreCase),
                                                        RdpFullScreenFlag = ERdpFullScreenFlag.EnableFullScreen,
                                                        DisplayPerformance = getValue("Colors")?.IndexOf("32") >= 0 ? EDisplayPerformance.High : EDisplayPerformance.Auto,
                                                        IsAdministrativePurposes = string.Equals(getValue("ConnectToConsole"), "TRUE", StringComparison.CurrentCultureIgnoreCase),
                                                        EnableClipboard = string.Equals(getValue("RedirectClipboard"), "TRUE", StringComparison.CurrentCultureIgnoreCase),
                                                        EnableDiskDrives = string.Equals(getValue("RedirectDiskDrives"), "TRUE", StringComparison.CurrentCultureIgnoreCase),
                                                        EnableKeyCombinations = string.Equals(getValue("RedirectKeys"), "TRUE", StringComparison.CurrentCultureIgnoreCase),
                                                        EnableSounds = string.Equals(getValue("BringToThisComputer"), "TRUE", StringComparison.CurrentCultureIgnoreCase),
                                                        EnableAudioCapture = string.Equals(getValue("RedirectAudioCapture"), "TRUE", StringComparison.CurrentCultureIgnoreCase),
                                                        EnablePorts = string.Equals(getValue("RedirectPorts"), "TRUE", StringComparison.CurrentCultureIgnoreCase),
                                                        EnablePrinters = string.Equals(getValue("RedirectPrinters"), "TRUE", StringComparison.CurrentCultureIgnoreCase),
                                                        EnableSmartCardsAndWinHello = string.Equals(getValue("RedirectSmartCards"), "TRUE", StringComparison.CurrentCultureIgnoreCase),
                                                        GatewayMode = string.Equals(getValue("RDGatewayUsageMethod"), "Never", StringComparison.CurrentCultureIgnoreCase) ? EGatewayMode.DoNotUseGateway :
                                                                            (string.Equals(getValue("RDGatewayUsageMethod"), "Detect", StringComparison.CurrentCultureIgnoreCase) ? EGatewayMode.AutomaticallyDetectGatewayServerSettings : EGatewayMode.UseTheseGatewayServerSettings),
                                                        GatewayHostName = getValue("RDGatewayHostname"),
                                                        GatewayPassword = getValue("RDGatewayPassword"),
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
                                                    server = new ProtocolServerVNC()
                                                    {
                                                        DispName = name,
                                                        GroupName = group,
                                                        Address = address,
                                                        Password = pwd,
                                                        Port = port.ToString(),
                                                    };
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
                                        EncryptPwd(serverBase);
                                        Server.AddOrUpdate(serverBase, true);
                                    }
                                    GlobalData.Instance.ServerListUpdate();
                                    MessageBox.Show(SystemConfig.Instance.Language.GetText("system_options_data_security_import_done").Replace("{0}", list.Count.ToString()), SystemConfig.Instance.Language.GetText("messagebox_title_info"), MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                                }
                                else
                                    MessageBox.Show(SystemConfig.Instance.Language.GetText("system_options_data_security_import_error"), SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                            }
                            catch (Exception e)
                            {
                                SimpleLogHelper.Debug(e, e.StackTrace);
                                MessageBox.Show(SystemConfig.Instance.Language.GetText("system_options_data_security_import_error"), SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
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
                                    MessageBox.Show(SystemConfig.Instance.Language.GetText("system_options_data_security_error_can_not_open"), SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                            }
                            catch (Exception ee)
                            {
                                SimpleLogHelper.Debug(ee, ee.StackTrace);
                                DbPath = oldDbPath;
                                MessageBox.Show(SystemConfig.Instance.Language.GetText("system_options_data_security_error_can_not_open"), SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
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
