using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using com.github.xiangyuecn.rsacsharp;
using PRM.Core.DB;
using PRM.Core.DB.Dapper;
using PRM.Core.I;
using PRM.Core.Protocol;
using PRM.Core.Protocol.Putty.SSH;
using PRM.Core.Protocol.RDP;

namespace PRM.Core.Service
{

    public class DataService : IDataService
    {
        private readonly IDb _db;

        public DataService()
        {
            //_db = new DapperDb();
            _db = new DapperDbFree();
        }

        public IDb DB()
        {
            return _db;
        }

        public bool Database_OpenConnection(DatabaseType type, string newConnectionString)
        {
            // open db connection, or create a sqlite db.
            Debug.Assert(_db != null);
            _db.OpenConnection(type, newConnectionString);

            // check database rsa encrypt
            var privateKeyPath = _db.GetFromDatabase_RSA_PrivateKeyPath();
            if (!string.IsNullOrWhiteSpace(privateKeyPath)
                && File.Exists(privateKeyPath))
            {
                _rsa = new RSA(File.ReadAllText(Database_GetPrivateKeyPath()), true);
            }
            else
            {
                _rsa = null;
            }
            return true;
        }

        public void Database_CloseConnection()
        {
            Debug.Assert(_db != null);
            if (_db.IsConnected())
                _db.CloseConnection();
        }

        public EnumDbStatus Database_SelfCheck()
        {
            _db?.OpenConnection();
            // check connection
            if (_db?.IsConnected() != true)
                return EnumDbStatus.NotConnected;

            // validate encryption
            var privateKeyPath = _db.GetFromDatabase_RSA_PrivateKeyPath();
            if (string.IsNullOrWhiteSpace(privateKeyPath))
            {
                // no encrypt
                return EnumDbStatus.OK;
            }
            var publicKey = _db.Get_RSA_PublicKey();
            var pks = RSA.CheckPrivatePublicKeyMatch(privateKeyPath, publicKey);
            switch (pks)
            {
                case RSA.EnumRsaStatus.CannotReadPrivateKeyFile:
                    return EnumDbStatus.RsaPrivateKeyNotFound;
                case RSA.EnumRsaStatus.PrivateKeyFormatError:
                    return EnumDbStatus.RsaPrivateKeyFormatError;
                case RSA.EnumRsaStatus.PublicKeyFormatError:
                    return EnumDbStatus.DataIsDamaged;
                case RSA.EnumRsaStatus.PrivateAndPublicMismatch:
                    return EnumDbStatus.RsaNotMatched;
                case RSA.EnumRsaStatus.NoError:
                    break;
            }
            return EnumDbStatus.OK;
        }

        private RSA _rsa = null;

        public string Database_GetPublicKey()
        {
            Debug.Assert(_db != null);
            return _db?.Get_RSA_PublicKey();
        }

        public string Database_GetPrivateKeyPath()
        {
            Debug.Assert(_db != null);
            return _db?.GetFromDatabase_RSA_PrivateKeyPath();
        }

        public RSA.EnumRsaStatus Database_SetEncryptionKey(string privateKeyPath, string privateKeyContent, IEnumerable<ProtocolServerBase> servers)
        {
            Debug.Assert(_db != null);

            // clear rsa key
            if (string.IsNullOrEmpty(privateKeyPath))
            {
                Debug.Assert(_rsa != null);
                Debug.Assert(string.IsNullOrWhiteSpace(Database_GetPrivateKeyPath()) == false);

                // decrypt
                var cloneList = new List<ProtocolServerBase>();
                foreach (var server in servers)
                {
                    var tmp = (ProtocolServerBase)server.Clone();
                    tmp.SetNotifyPropertyChangedEnabled(false);
                    DecryptToConnectLevel(ref tmp);
                    cloneList.Add(tmp);
                }

                // update 
                if (_db.SetRsa("", "", cloneList))
                {
                    _rsa = null;
                }
                return RSA.EnumRsaStatus.NoError;
            }
            // set rsa key
            else
            {
                Debug.Assert(_rsa == null);
                Debug.Assert(string.IsNullOrWhiteSpace(Database_GetPrivateKeyPath()) == true);

                
                var pks = RSA.KeyCheck(privateKeyContent, true);
                if (pks != RSA.EnumRsaStatus.NoError)
                    return pks;
                var rsa = new RSA(privateKeyContent, true);

                // encrypt
                var cloneList = new List<ProtocolServerBase>();
                foreach (var server in servers)
                {
                    var tmp = (ProtocolServerBase)server.Clone();
                    tmp.SetNotifyPropertyChangedEnabled(false);
                    EncryptToDatabaseLevel(rsa, ref tmp);
                    cloneList.Add(tmp);
                }

                // update 
                if (_db.SetRsa(privateKeyPath, rsa.ToPEM_PKCS1(true), cloneList))
                {
                    _db.Set_RSA_SHA1(rsa.Sign("SHA1", ConfigurationService.AppName));
                    _rsa = rsa;
                }
                return RSA.EnumRsaStatus.NoError;
            }
        }

        public RSA.EnumRsaStatus Database_UpdatePrivateKeyPathOnly(string privateKeyPath)
        {
            Debug.Assert(_rsa != null);
            Debug.Assert(string.IsNullOrWhiteSpace(Database_GetPrivateKeyPath()) == false);
            Debug.Assert(File.Exists(privateKeyPath));

            var pks = RSA.CheckPrivatePublicKeyMatch(privateKeyPath, Database_GetPublicKey());
            if (pks == RSA.EnumRsaStatus.NoError)
            {
                _db.Set_RSA_PrivateKeyPath(privateKeyPath);
            }
            return pks;
        }

        public string DecryptOrReturnOriginalString(string originalString)
        {
            return _rsa?.DecodeOrNull(originalString) ?? originalString;
        }

        public static string Encrypt(RSA rsa, string str)
        {
            if (rsa.DecodeOrNull(str) == null)
                return rsa.Encode(str);
            return str;
        }

        public string Encrypt(string str)
        {
            return Encrypt(_rsa, str);
        }

        public static void EncryptToDatabaseLevel(RSA rsa, ref ProtocolServerBase server)
        {
            if (rsa == null) return;
            // ! server must be decrypted
            for (var i = 0; i < server.Tags.Count; i++)
            {
                server.Tags[i] = Encrypt(rsa, server.Tags[i]);
            }

            // encrypt some info
            if (server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortBase)))
            {
                var p = (ProtocolServerWithAddrPortBase)server;
                p.Address = Encrypt(rsa, p.Address);
                p.Port = Encrypt(rsa, p.Port);
            }
            if (server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase)))
            {
                var p = (ProtocolServerWithAddrPortUserPwdBase)server;
                p.UserName = Encrypt(rsa, p.UserName);
            }


            // encrypt password
            if (server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase)))
            {
                var s = (ProtocolServerWithAddrPortUserPwdBase)server;
                s.Password = Encrypt(rsa, s.Password);
            }
            switch (server)
            {
                case ProtocolServerSSH ssh when !string.IsNullOrWhiteSpace(ssh.PrivateKey):
                    {
                        ssh.PrivateKey = Encrypt(rsa, ssh.PrivateKey);
                        break;
                    }
                case ProtocolServerRDP rdp when !string.IsNullOrWhiteSpace(rdp.GatewayPassword):
                    {
                        rdp.GatewayPassword = Encrypt(rsa, rdp.GatewayPassword);
                        break;
                    }
            }
        }
        public void EncryptToDatabaseLevel(ref ProtocolServerBase server)
        {
            EncryptToDatabaseLevel(_rsa, ref server);
        }

        public void DecryptToRamLevel(ref ProtocolServerBase server)
        {
            if (_rsa == null) return;
            server.DisplayName = DecryptOrReturnOriginalString(server.DisplayName);
            for (var i = 0; i < server.Tags.Count; i++)
            {
                server.Tags[i] = DecryptOrReturnOriginalString(server.Tags[i]);
            }

            if (server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortBase)))
            {
                var p = (ProtocolServerWithAddrPortBase)server;
                p.Address = DecryptOrReturnOriginalString(p.Address);
                p.Port = DecryptOrReturnOriginalString(p.Port);
            }

            if (server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase)))
            {
                var p = (ProtocolServerWithAddrPortUserPwdBase)server;
                p.UserName = DecryptOrReturnOriginalString(p.UserName);
            }
        }

        public void DecryptToConnectLevel(ref ProtocolServerBase server)
        {
            if (_rsa == null) return;
            DecryptToRamLevel(ref server);
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

        public void Database_InsertServer(ProtocolServerBase server)
        {
            var tmp = (ProtocolServerBase)server.Clone();
            tmp.SetNotifyPropertyChangedEnabled(false);
            EncryptToDatabaseLevel(ref tmp);
            _db.AddServer(tmp);
        }

        public void Database_InsertServer(IEnumerable<ProtocolServerBase> servers)
        {
            var cloneList = new List<ProtocolServerBase>();
            foreach (var server in servers)
            {
                var tmp = (ProtocolServerBase)server.Clone();
                tmp.SetNotifyPropertyChangedEnabled(false);
                EncryptToDatabaseLevel(ref tmp);
                cloneList.Add(tmp);
            }
            _db.AddServer(cloneList);
        }

        public bool Database_UpdateServer(ProtocolServerBase org)
        {
            Debug.Assert(org.Id > 0);
            var tmp = (ProtocolServerBase)org.Clone();
            tmp.SetNotifyPropertyChangedEnabled(false);
            EncryptToDatabaseLevel(ref tmp);
            return _db.UpdateServer(tmp);
        }

        public bool Database_UpdateServer(IEnumerable<ProtocolServerBase> servers)
        {
            var cloneList = new List<ProtocolServerBase>();
            foreach (var server in servers)
            {
                var tmp = (ProtocolServerBase)server.Clone();
                tmp.SetNotifyPropertyChangedEnabled(false);
                EncryptToDatabaseLevel(ref tmp);
                cloneList.Add(tmp);
            }
            return _db.UpdateServer(cloneList);
        }

        public bool Database_DeleteServer(int id)
        {
            return _db.DeleteServer(id);
        }

        public bool Database_DeleteServer(IEnumerable<int> ids)
        {
            return _db.DeleteServer(ids);
        }

        public List<ProtocolServerBase> Database_GetServers()
        {
            return _db.GetServers();
        }
    }
}