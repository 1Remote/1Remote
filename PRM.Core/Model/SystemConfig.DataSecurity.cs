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
using System.Threading;
using com.github.xiangyuecn.rsacsharp;
using Microsoft.Win32;
using PRM.Core.DB;
using PRM.Core.Protocol;
using PRM.Core.Protocol.Putty.SSH;
using PRM.Core.Protocol.RDP;
using Shawn.Ulits;

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

        public enum ERsaStatues
        {
            Ok,
            CanNotFindPrivateKey,
            PrivateKeyContentError,
            PrivateKeyIsNotMatch,
        }
        public ERsaStatues ValidateRsa(string privateKeyPath = "")
        {
            var _privateKeyPath = DB.Config.RSA_PrivateKeyPath;
            if (!string.IsNullOrEmpty(privateKeyPath))
                _privateKeyPath = privateKeyPath;

            // NO RSA
            if (string.IsNullOrEmpty(DB.Config.RSA_PublicKey)
                && string.IsNullOrEmpty(_privateKeyPath)
                && string.IsNullOrEmpty(DB.Config.RSA_SHA1))
                return ERsaStatues.Ok;

            if (!File.Exists(_privateKeyPath))
                return ERsaStatues.CanNotFindPrivateKey;

            com.github.xiangyuecn.rsacsharp.RSA rsaPk = null;
            com.github.xiangyuecn.rsacsharp.RSA rsaPpk = null;

            try
            {
                rsaPpk = new com.github.xiangyuecn.rsacsharp.RSA(File.ReadAllText(_privateKeyPath), true);
            }
            catch (Exception)
            {
                return ERsaStatues.PrivateKeyContentError;
            }

            // make sure public key is PEM format key
            try
            {
                rsaPk = new com.github.xiangyuecn.rsacsharp.RSA(DB.Config.RSA_PublicKey, true);
            }
            catch (Exception)
            {
                // try to fix public key
                if (rsaPpk.Verify("SHA1", DB.Config.RSA_SHA1, SystemConfig.AppName))
                {
                    DB.Config.RSA_PublicKey = rsaPpk.ToPEM_PKCS1(true);
                    rsaPk = new com.github.xiangyuecn.rsacsharp.RSA(File.ReadAllText(_privateKeyPath), true);
                    return ERsaStatues.Ok;
                }
            }
            // RSA private key is match public key?
            try
            {
                rsaPpk = new com.github.xiangyuecn.rsacsharp.RSA(File.ReadAllText(_privateKeyPath), true);
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
                return ERsaStatues.PrivateKeyIsNotMatch;
            }

            return ERsaStatues.Ok;
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
                    _dbPath = Path.Combine(appDateFolder, "PRemoteM.db");
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
            set
            {
                DB.Config.RSA_PrivateKeyPath = value;
                Rsa = null;
            }
        }

        private com.github.xiangyuecn.rsacsharp.RSA _rsa = null;
        public com.github.xiangyuecn.rsacsharp.RSA Rsa
        {
            set => _rsa = value;
            get
            {
                if (_rsa == null)
                {
                    var ret = ValidateRsa();
                    switch (ret)
                    {
                        case ERsaStatues.Ok:
                            break;
                        case ERsaStatues.CanNotFindPrivateKey:
                            throw new Exception(SystemConfig.GetInstance().Language.GetText("system_options_data_security_error_rsa_private_key_not_found"));
                            break;
                        case ERsaStatues.PrivateKeyContentError:
                            throw new Exception(SystemConfig.GetInstance().Language.GetText("system_options_data_security_error_rsa_private_key_not_match"));
                            break;
                        case ERsaStatues.PrivateKeyIsNotMatch:
                            throw new Exception(SystemConfig.GetInstance().Language.GetText("system_options_data_security_error_rsa_private_key_not_match"));
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                    if (string.IsNullOrEmpty(DB.Config.RSA_PublicKey))
                        return null;
                    _rsa = new com.github.xiangyuecn.rsacsharp.RSA(File.ReadAllText(DB.Config.RSA_PrivateKeyPath), true);
                }
                return _rsa;
            }
        }

        /// <summary>
        /// Invoke Progress bar percent = arg1 / arg2
        /// </summary>
        public Action<int, int> OnRsaProgress = null;
        private readonly object _lockerForRsa = new object();
        public const string PrivateKeyFileExt = ".prpk";
        public void GenRsa()
        {
            if (ValidateRsa() != ERsaStatues.Ok)
            {
                throw new Exception(SystemConfig.GetInstance().Language.GetText("system_options_data_security_error_rsa_private_key_not_match"));
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
                            var protocolServerBases = Global.GetInstance().ServerList;
                            int max = protocolServerBases.Count() * 3 + 2;
                            int val = 0;

                            var dlg = new SaveFileDialog
                            {
                                Title = SystemConfig.GetInstance().Language.GetText("system_options_data_security_rsa_encrypt_dialog_title"),
                                Filter = $"PRM RSA private key|*{Core.Model.SystemConfigDataSecurity.PrivateKeyFileExt}",
                                FileName = SystemConfig.AppName + "_" + DateTime.Now.ToString("yyyyMMddhhmmss") +
                                           Core.Model.SystemConfigDataSecurity.PrivateKeyFileExt,
                            };
                            if (dlg.ShowDialog() == true)
                            {
                                OnRsaProgress?.Invoke(val, max);
                                // database back up
                                Debug.Assert(File.Exists(DbPath));
                                File.Copy(_dbPath, _dbPath + ".back", true);
                                OnRsaProgress?.Invoke(++val, max);


                                // gen rsa
                                var rsa = new com.github.xiangyuecn.rsacsharp.RSA(2048);
                                OnRsaProgress?.Invoke(++val, max);
                                Rsa = null;

                                // save key
                                DB.Config.RSA_SHA1 = rsa.Sign("SHA1", SystemConfig.AppName);
                                DB.Config.RSA_PublicKey = rsa.ToPEM_PKCS1(true);
                                DB.Config.RSA_PrivateKeyPath = dlg.FileName;
                                File.WriteAllText(dlg.FileName, rsa.ToPEM_PKCS1());
                                RaisePropertyChanged(nameof(RsaPublicKey));
                                RaisePropertyChanged(nameof(RsaPrivateKeyPath));


                                // encrypt old data
                                foreach (var psb in protocolServerBases)
                                {
                                    EncryptPwd(psb);
                                    OnRsaProgress?.Invoke(++val, max);
                                    Server.AddOrUpdate(psb);
                                    OnRsaProgress?.Invoke(++val, max);
                                }

                                // del back up
                                File.Delete(_dbPath + ".back");

                                // done
                                OnRsaProgress?.Invoke(0, 0);
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
            var ret = ValidateRsa();
            switch (ret)
            {
                case ERsaStatues.Ok:
                    break;
                case ERsaStatues.CanNotFindPrivateKey:
                    throw new Exception(SystemConfig.GetInstance().Language.GetText("system_options_data_security_error_rsa_private_key_not_found"));
                    break;
                case ERsaStatues.PrivateKeyContentError:
                    throw new Exception(SystemConfig.GetInstance().Language.GetText("system_options_data_security_error_rsa_private_key_not_match"));
                    break;
                case ERsaStatues.PrivateKeyIsNotMatch:
                    throw new Exception(SystemConfig.GetInstance().Language.GetText("system_options_data_security_error_rsa_private_key_not_match"));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (!string.IsNullOrEmpty(DB.Config.RSA_PublicKey))
            {
                var t = new Task(() =>
                {
                    lock (_lockerForRsa)
                    {
                        if (!string.IsNullOrEmpty(DB.Config.RSA_PublicKey))
                        {
                            OnRsaProgress?.Invoke(0, 1);
                            var protocolServerBases = Global.GetInstance().ServerList;
                            int max = protocolServerBases.Count() * 3 + 2 + 1;
                            int val = 1;
                            OnRsaProgress?.Invoke(val, max);

                            // database back up
                            Debug.Assert(File.Exists(DbPath));
                            File.Copy(_dbPath, _dbPath + ".back", true);
                            OnRsaProgress?.Invoke(++val, max);

                            // keep pld rsa
                            var ppkPath = DB.Config.RSA_PrivateKeyPath;
                            //var rsa = new com.github.xiangyuecn.rsacsharp.RSA(File.ReadAllText(DB.Config.RSA_PrivateKeyPath), true);


                            // decrypt pwd
                            Debug.Assert(Rsa != null);
                            foreach (var psb in protocolServerBases)
                            {
                                DecryptPwd(psb);
                                OnRsaProgress?.Invoke(++val, max);
                            }

                            // remove rsa keys from db
                            Rsa = null;
                            DB.Config.RSA_SHA1 = "";
                            DB.Config.RSA_PublicKey = "";
                            DB.Config.RSA_PrivateKeyPath = "";
                            
                            // update
                            foreach (var psb in protocolServerBases)
                            {
                                Server.AddOrUpdate(psb);
                                OnRsaProgress?.Invoke(++val, max);
                            }


                            RaisePropertyChanged(nameof(RsaPublicKey));
                            RaisePropertyChanged(nameof(RsaPrivateKeyPath));

                            // del key
                            File.Delete(ppkPath);

                            // del back up
                            File.Delete(_dbPath + ".back");

                            // done
                            OnRsaProgress?.Invoke(0, 0);
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
                switch (server)
                {
                    case ProtocolServerRDP rdp:
                        Debug.Assert(rsa.DecodeOrNull(rdp.Password) == null);
                        rdp.Password = rsa.Encode(rdp.Password);
                        break;
                    case ProtocolServerSSH ssh:
                        if (!string.IsNullOrEmpty(ssh.Password))
                        {
                            Debug.Assert(rsa.DecodeOrNull(ssh.Password) == null);
                            ssh.Password = rsa.Encode(ssh.Password);
                        }
                        if (!string.IsNullOrEmpty(ssh.PrivateKey))
                        {
                            Debug.Assert(rsa.DecodeOrNull(ssh.PrivateKey) == null);
                            ssh.PrivateKey = rsa.Encode(ssh.PrivateKey);
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException($"Protocol not support: {server.GetType()}");
                }
        }

        public void DecryptPwd(ProtocolServerBase server)
        {
            var rsa = Rsa;
            if (rsa != null)
            {
                switch (server)
                {
                    case ProtocolServerRDP rdp:
                        Debug.Assert(rsa.DecodeOrNull(rdp.Password) != null);
                        rdp.Password = rsa.DecodeOrNull(rdp.Password);
                        break;
                    case ProtocolServerSSH ssh:
                        if (!string.IsNullOrEmpty(ssh.Password))
                        {
                            Debug.Assert(rsa.DecodeOrNull(ssh.Password) != null);
                            ssh.Password = rsa.DecodeOrNull(ssh.Password);
                        }
                        if (!string.IsNullOrEmpty(ssh.PrivateKey))
                        {
                            Debug.Assert(rsa.DecodeOrNull(ssh.PrivateKey) != null);
                            ssh.PrivateKey = rsa.DecodeOrNull(ssh.PrivateKey);
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException($"Protocol not support: {server.GetType()}");
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
                    case ProtocolServerNone _:
                        break;
                    case ProtocolServerRDP _:
                    case ProtocolServerSSH _:
                        var p = (ProtocolServerWithAddrPortUserPwdBase) server;
                        if (!string.IsNullOrEmpty(p.UserName))
                            p.UserName = rsa.Encode(p.UserName);
                        if (!string.IsNullOrEmpty(p.Address))
                            p.Address = rsa.Encode(p.Address);
                        if (!string.IsNullOrEmpty(p.Password))
                            p.Password = rsa.Encode(p.Password);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException($"Protocol not support: {server.GetType()}");
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
                    case ProtocolServerNone _:
                        break;
                    case ProtocolServerRDP _:
                    case ProtocolServerSSH _:
                        var p = (ProtocolServerWithAddrPortUserPwdBase) server;
                        if (!string.IsNullOrEmpty(p.UserName))
                            p.UserName = rsa.DecodeOrNull(p.UserName);
                        if (!string.IsNullOrEmpty(p.Address))
                            p.Address = rsa.DecodeOrNull(p.Address);
                        if (!string.IsNullOrEmpty(p.Password))
                            p.Password = rsa.DecodeOrNull(p.Password);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException($"Protocol not support: {server.GetType()}");
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
            DbPath = _ini.GetValue(nameof(DbPath).ToLower(), SectionName, DbPath);
            StopAutoSave = false;
        }

        public override void Update(SystemConfigBase newConfig)
        {
            UpdateBase(this, newConfig, typeof(SystemConfigDataSecurity));
            Save();
        }

        #endregion
    }
}
