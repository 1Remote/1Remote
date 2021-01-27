using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using com.github.xiangyuecn.rsacsharp;
using PRM.Core.DB;
using PRM.Core.Protocol;
using PRM.Core.Protocol.Putty.SSH;
using PRM.Core.Protocol.RDP;
using Shawn.Utils;

namespace PRM.Core.Model
{
    public class DbOperator
    {
        private readonly IDb _db;
        public DbOperator(IDb db)
        {
            _db = db;
            var privateKeyPath = _db.Get_RSA_PrivateKeyPath();
            if (!string.IsNullOrWhiteSpace(privateKeyPath)
                && File.Exists(privateKeyPath))
            {
                _rsa = new RSA(File.ReadAllText(privateKeyPath), true);
            }
        }

        /// <summary>
        /// return:
        /// 0: ok
        /// -1: private key not existed
        /// -2: private key is not in correct format
        /// -3: private key not matched
        /// </summary>
        /// <returns></returns>
        public int CheckDbRsaIsOk()
        {
            var rsaPrivateKeyPath = _db.Get_RSA_PrivateKeyPath();

            // NO RSA
            if (string.IsNullOrEmpty(rsaPrivateKeyPath))
                return 0;

            var sha1 = _db.Get_RSA_SHA1();
            var rsaPublicKey = _db.Get_RSA_PublicKey();

            if (!File.Exists(rsaPrivateKeyPath))
                return -1;

            RSA rsa = null;
            try
            {
                rsa = new RSA(File.ReadAllText(rsaPrivateKeyPath), true);
            }
            catch (Exception)
            {
                return -2;
            }



            // make sure public key is PEM format key
            RSA rsaPublicKeyObj = null;
            try
            {
                rsaPublicKeyObj = new RSA(rsaPublicKey, true);
            }
            catch (Exception)
            {
                // try to fix public key
                if (rsa.Verify("SHA1", sha1, SystemConfig.AppName))
                {
                    this._db.Set_RSA_PublicKey(rsa.ToPEM_PKCS1(true));
                    rsaPublicKeyObj = new RSA(File.ReadAllText(rsaPrivateKeyPath), true);
                }
            }

            // check if RSA private key is matched public key?
            try
            {
                rsa = new RSA(File.ReadAllText(rsaPrivateKeyPath), true);
                var sha1Tmp = rsa.Sign("SHA1", SystemConfig.AppName);
                if (rsaPublicKeyObj?.Verify("SHA1", sha1Tmp, SystemConfig.AppName) != true)
                {
                    throw new Exception("RSA key is not match!");
                }
                this._db.Set_RSA_SHA1(sha1Tmp);
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
                return -3;
            }


            return 0;
        }

        private RSA _rsa = null;

        private string GetRsaPublicKey()
        {
            return _db.Get_RSA_PublicKey();
        }

        public string GetRsaPrivateKeyPath()
        {
            return _db.Get_RSA_PrivateKeyPath();
        }

        private void ClearRsaPrivateKey()
        {
            _db.Set_RSA_SHA1("");
            _db.Set_RSA_PublicKey("");
            _db.Set_RSA_PrivateKeyPath("");
        }

        /// <summary>
        /// return
        /// 0: success
        /// -1: DB is encrypted
        /// -2: private key is not in correct format
        /// </summary>
        /// <param name="privateKeyPath"></param>
        /// <returns></returns>
        public int SetRsaPrivateKey(string privateKeyPath)
        {
            if (string.IsNullOrWhiteSpace(privateKeyPath))
            {
                ClearRsaPrivateKey();
                return 0;
            }

            Debug.Assert(File.Exists(privateKeyPath));
            var oldPath = GetRsaPrivateKeyPath();
            if (!string.IsNullOrWhiteSpace(oldPath))
                return -1;

            RSA rsa = null;
            try
            {
                rsa = new RSA(File.ReadAllText(privateKeyPath), true);
            }
            catch (Exception e)
            {
                SimpleLogHelper.Debug(e);
                return -2;
            }

            _rsa = rsa;
            _db.Set_RSA_SHA1(rsa.Sign("SHA1", SystemConfig.AppName));
            _db.Set_RSA_PublicKey(rsa.ToPEM_PKCS1(true));
            _db.Set_RSA_PrivateKeyPath(privateKeyPath);
            return 0;
        }

        public void EncryptPwdIfItIsNotEncrypted(ProtocolServerBase server)
        {
            if (_rsa == null) return;
            if (server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase)))
            {
                var s = (ProtocolServerWithAddrPortUserPwdBase)server;
                if (_rsa.DecodeOrNull(s.Password) == null)
                    s.Password = _rsa.Encode(s.Password);
            }
            switch (server)
            {
                case ProtocolServerSSH ssh when !string.IsNullOrWhiteSpace(ssh.PrivateKey):
                {
                    if (_rsa.DecodeOrNull(ssh.PrivateKey) == null)
                        ssh.PrivateKey = _rsa.Encode(ssh.PrivateKey);
                    break;
                }
                case ProtocolServerRDP rdp when !string.IsNullOrWhiteSpace(rdp.GatewayPassword):
                {
                    if (_rsa.DecodeOrNull(rdp.GatewayPassword) == null)
                        rdp.GatewayPassword = _rsa.Encode(rdp.GatewayPassword);
                    break;
                }
            }
        }

        public string DecryptOrReturnOriginalString(string originalString)
        {
            if (_rsa == null) return originalString;
            return _rsa.DecodeOrNull(originalString) ?? originalString;
        }

        public void DecryptPwdIfItIsEncrypted(ProtocolServerBase server)
        {
            if (_rsa == null) return;
            if (server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase)))
            {
                var s = (ProtocolServerWithAddrPortUserPwdBase)server;
                s.Password = DecryptOrReturnOriginalString(s.Password);
            }
            switch (server)
            {
                case ProtocolServerSSH ssh when !string.IsNullOrWhiteSpace(ssh.PrivateKey):
                    Debug.Assert(_rsa.DecodeOrNull(ssh.PrivateKey) != null);
                    ssh.PrivateKey = DecryptOrReturnOriginalString(ssh.PrivateKey);
                    break;
                case ProtocolServerRDP rdp when !string.IsNullOrWhiteSpace(rdp.GatewayPassword):
                    Debug.Assert(_rsa.DecodeOrNull(rdp.GatewayPassword) != null);
                    rdp.GatewayPassword = DecryptOrReturnOriginalString(rdp.GatewayPassword);
                    break;
            }
        }

        public void EncryptInfo(ProtocolServerBase server)
        {
            if (_rsa == null) return;
            Debug.Assert(_rsa.DecodeOrNull(server.DispName) == null);
            server.DispName = _rsa.Encode(server.DispName);
            server.GroupName = _rsa.Encode(server.GroupName);

            if (server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortBase)))
            {
                var p = (ProtocolServerWithAddrPortUserPwdBase)server;
                if (!string.IsNullOrEmpty(p.UserName))
                    p.UserName = _rsa.Encode(p.UserName);
                if (!string.IsNullOrEmpty(p.Address))
                    p.Address = _rsa.Encode(p.Address);
            }
        }

        public void DecryptInfo(ProtocolServerBase server)
        {
            if (_rsa == null) return;
            Debug.Assert(_rsa.DecodeOrNull(server.DispName) != null);
            server.DispName = DecryptOrReturnOriginalString(server.DispName);
            server.GroupName = DecryptOrReturnOriginalString(server.GroupName);

            if (server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortBase)))
            {
                var p = (ProtocolServerWithAddrPortUserPwdBase)server;
                if (!string.IsNullOrEmpty(p.UserName))
                    p.UserName = DecryptOrReturnOriginalString(p.UserName) ?? p.UserName;
                if (!string.IsNullOrEmpty(p.Address))
                    p.Address = DecryptOrReturnOriginalString(p.Address) ?? p.Address;
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
    }
}
