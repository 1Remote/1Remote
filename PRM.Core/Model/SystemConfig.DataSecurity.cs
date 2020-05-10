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
                // TODO 删除 back 文件
            }

            //// validate rsa key
            //if (ValidateRsa() == false)
            //{
            //    // TODO 提示公钥/私钥不匹配，弹出选择界面重新选择
            //    //SimpleLogHelper.Error("Your ");
            //}
        }

        public enum ERsaStatues
        {
            Ok,
            CanNotFindPrivateKey,
            PrivateKeyContentError,
            PrivateKeyIsNotMatch,
        }
        public ERsaStatues ValidateRsa()
        {
            // NO RSA
            if (string.IsNullOrEmpty(DB.Config.RSA_PublicKey)
                && string.IsNullOrEmpty(DB.Config.RSA_PrivateKeyPath)
                && string.IsNullOrEmpty(DB.Config.RSA_SHA1))
                return ERsaStatues.Ok;

            if (!File.Exists(DB.Config.RSA_PrivateKeyPath))
                return ERsaStatues.CanNotFindPrivateKey;

            com.github.xiangyuecn.rsacsharp.RSA rsaPk = null;
            com.github.xiangyuecn.rsacsharp.RSA rsaPpk = null;

            try
            {
                rsaPpk = new com.github.xiangyuecn.rsacsharp.RSA(File.ReadAllText(DB.Config.RSA_PrivateKeyPath), true);
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
                    rsaPk = new com.github.xiangyuecn.rsacsharp.RSA(File.ReadAllText(DB.Config.RSA_PrivateKeyPath), true);
                    return ERsaStatues.Ok;
                }
            }
            // RSA private key is match public key?
            try
            {
                rsaPpk = new com.github.xiangyuecn.rsacsharp.RSA(File.ReadAllText(DB.Config.RSA_PrivateKeyPath), true);
                var sha1 = rsaPpk.Sign("SHA1", SystemConfig.AppName);
                if (!rsaPk.Verify("SHA1", sha1, SystemConfig.AppName))
                {
                    throw new Exception("RSA key is not match!");
                }
                DB.Config.RSA_SHA1 = sha1;
            }
            catch (Exception e)
            {
                // TODO 提示公钥/私钥不匹配，弹出选择界面重新选择
                SimpleLogHelper.Error("Your ");
                SimpleLogHelper.Error(e);
                return ERsaStatues.PrivateKeyIsNotMatch;
            }

            return ERsaStatues.Ok;
        }

        private string _dbPath = "./PRemoteM.db";
        public string DbPath
        {
            get => _dbPath;
            set => SetAndNotifyIfChanged(nameof(DbPath), ref _dbPath, value.Replace(Environment.CurrentDirectory, "."));
        }

        public string RsaPublicKey => DB.Config.RSA_PublicKey;

        public string RsaPrivateKeyPath => DB.Config.RSA_PrivateKeyPath;

        public com.github.xiangyuecn.rsacsharp.RSA Rsa
        {
            get
            {
                if (ValidateRsa() != ERsaStatues.Ok)
                {
                    throw new Exception("Rsa key is not match!");
                }
                if (string.IsNullOrEmpty(DB.Config.RSA_PublicKey))
                    return null;
                return new com.github.xiangyuecn.rsacsharp.RSA(File.ReadAllText(DB.Config.RSA_PrivateKeyPath), true);
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
            var t = new Task(() =>
            {
                if (string.IsNullOrEmpty(DB.Config.RSA_PublicKey))
                    lock (_lockerForRsa)
                    {
                        if (string.IsNullOrEmpty(DB.Config.RSA_PublicKey))
                        {
                            var servers = PRM.Core.DB.Server.ListAll();
                            int max = servers.Count * 2 + 2;
                            int val = 0;

                            var dlg = new SaveFileDialog
                            {
                                Title = "TXT: 选择私钥放置位置",
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

                                //// encrypt old data & show process bar
                                //foreach (var server in servers)
                                //{
                                //    // todo encrypt pwd
                                //    server.JsonConfigString = rsa.Encode(server.JsonConfigString);
                                //    OnRsaProgress?.Invoke(++val, max);
                                //}
                                //foreach (var server in servers)
                                //{
                                //    server.Update();
                                //    OnRsaProgress?.Invoke(++val, max);
                                //}

                                DB.Config.RSA_SHA1 = rsa.Sign("SHA1", SystemConfig.AppName);
                                DB.Config.RSA_PublicKey = rsa.ToPEM_PKCS1(true);
                                DB.Config.RSA_PrivateKeyPath = dlg.FileName;
                                RaisePropertyChanged(nameof(RsaPublicKey));
                                RaisePropertyChanged(nameof(RsaPrivateKeyPath));

                                // save key
                                File.WriteAllText(dlg.FileName, rsa.ToPEM_PKCS1());

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
        public void CleanRsa()
        {
            // validate rsa key
            if (ValidateRsa() == ERsaStatues.Ok)
            {
                // TODO 提示公钥/私钥不匹配，弹出选择界面重新选择
                //SimpleLogHelper.Error("Your ");
            }

            var t = new Task(() =>
            {
                if (!string.IsNullOrEmpty(DB.Config.RSA_PublicKey))
                    lock (_lockerForRsa)
                    {
                        if (!string.IsNullOrEmpty(DB.Config.RSA_PublicKey))
                        {
                            var servers = PRM.Core.DB.Server.ListAll();
                            int max = servers.Count * 2 + 2;
                            int val = 0;
                            OnRsaProgress?.Invoke(val, max);

                            // database back up
                            Debug.Assert(File.Exists(DbPath));
                            File.Copy(_dbPath, _dbPath + ".back", true);
                            OnRsaProgress?.Invoke(++val, max);

                            // gen rsa
                            var ppkPath = DB.Config.RSA_PrivateKeyPath;
                            var rsa = Rsa;

                            OnRsaProgress?.Invoke(++val, max);

                            Thread.Sleep(200);
                            OnRsaProgress?.Invoke(++val, max);
                            Thread.Sleep(200);
                            OnRsaProgress?.Invoke(++val, max);
                            Thread.Sleep(200);
                            OnRsaProgress?.Invoke(++val, max);
                            Thread.Sleep(200);
                            OnRsaProgress?.Invoke(++val, max);


                            // decrypt old data &show process bar
                            //foreach (var server in servers)
                            //{
                            //    // todo decrypt pwd
                            //    server.JsonConfigString = rsa.DecodeOrNull(server.JsonConfigString);
                            //    Debug.Assert(server.JsonConfigString != null);
                            //    OnRsaProgress?.Invoke(++val, max);
                            //}
                            //foreach (var server in servers)
                            //{
                            //    server.Update();
                            //    OnRsaProgress?.Invoke(++val, max);
                            //}


                            DB.Config.RSA_SHA1 = "";
                            DB.Config.RSA_PublicKey = "";
                            DB.Config.RSA_PrivateKeyPath = "";
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


        #region Interface
        private const string SectionName = "DataSecurity";
        public override void Save()
        {
            _ini.WriteValue(nameof(DbPath).ToLower(), SectionName, DbPath);
            _ini.Save();
        }

        public override void Load()
        {
            //TODO thread not safe
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
