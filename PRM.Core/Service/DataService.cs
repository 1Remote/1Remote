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
    public class DataService
    {
        private readonly IDb _db;

        public DataService()
        {
            _db = new DapperDb();
        }

        public bool Database_OpenConnection(DatabaseType? type = null, string newConnectionString = "")
        {
            // open db connection
            Debug.Assert(_db != null);
            _db.OpenConnection(type, newConnectionString);

            // check database rsa encrypt
            if (Database_IsEncrypted()
                && File.Exists(Database_GetPrivateKeyPath()))
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

        public bool Database_IsEncrypted()
        {
            Debug.Assert(_db != null);
            var rsaPrivateKeyPath = _db.GetFromDatabase_RSA_PrivateKeyPath();
            return !string.IsNullOrWhiteSpace(rsaPrivateKeyPath);
        }

        public EnumDbStatus Database_SelfCheck()
        {
            // check connection
            if (_db?.IsConnected() != true)
                return EnumDbStatus.NotConnected;

            // check encrypt
            if (Database_IsEncrypted() == false)
                return EnumDbStatus.OK;

            // validate encryption
            var privateKeyPath = _db.GetFromDatabase_RSA_PrivateKeyPath();
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


        public RSA.EnumRsaStatus Database_SetEncryptionKey(string privateKeyPath, bool generateNewPublicKey = true)
        {
            Debug.Assert(_db != null);
            Debug.Assert(Database_IsEncrypted() == false);

            if (string.IsNullOrEmpty(privateKeyPath))
            {
                _db.Set_RSA_PublicKey("");
                _db.Set_RSA_PrivateKeyPath("");
                return RSA.EnumRsaStatus.NoError;
            }

            var pks = RSA.KeyFileCheck(privateKeyPath, true);
            if (pks != RSA.EnumRsaStatus.NoError)
                return pks;

            var rsa = new RSA(File.ReadAllText(privateKeyPath), true);
            _db.Set_RSA_SHA1(rsa.Sign("SHA1", ConfigurationService.AppName));
            if (generateNewPublicKey)
                _db.Set_RSA_PublicKey(rsa.ToPEM_PKCS1(true));
            _db.Set_RSA_PrivateKeyPath(privateKeyPath);
            _rsa = rsa;
            return RSA.EnumRsaStatus.NoError;
        }

        public string DecryptOrReturnOriginalString(string originalString)
        {
            return _rsa?.DecodeOrNull(originalString) ?? originalString;
        }

        public string Encrypt(string str)
        {
            if (_rsa.DecodeOrNull(str) == null)
                return _rsa.Encode(str);
            return str;
        }

        public void EncryptToDatabaseLevel(ref ProtocolServerBase server)
        {
            if (_rsa == null) return;
            Debug.Assert(Database_IsEncrypted() == true);
            Debug.Assert(_rsa.DecodeOrNull(server.DisplayName) == null);
            // ! server must be decrypted
            for (var i = 0; i < server.Tags.Count; i++)
            {
                server.Tags[i] = Encrypt(server.Tags[i]);
            }

            // encrypt some info
            if (server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortBase)))
            {
                var p = (ProtocolServerWithAddrPortBase)server;
                p.Address = Encrypt(p.Address);
                p.Port = Encrypt(p.Port);
            }
            if (server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase)))
            {
                var p = (ProtocolServerWithAddrPortUserPwdBase)server;
                p.UserName = Encrypt(p.UserName);
            }


            // encrypt password
            if (server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase)))
            {
                var s = (ProtocolServerWithAddrPortUserPwdBase)server;
                s.Password = Encrypt(s.Password);
            }
            switch (server)
            {
                case ProtocolServerSSH ssh when !string.IsNullOrWhiteSpace(ssh.PrivateKey):
                    {
                        ssh.PrivateKey = Encrypt(ssh.PrivateKey);
                        break;
                    }
                case ProtocolServerRDP rdp when !string.IsNullOrWhiteSpace(rdp.GatewayPassword):
                    {
                        rdp.GatewayPassword = Encrypt(rdp.GatewayPassword);
                        break;
                    }
            }
        }

        public void DecryptToRamLevel(ProtocolServerBase server)
        {
            if (_rsa == null) return;
            Debug.Assert(_rsa.DecodeOrNull(server.DisplayName) != null);
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

        public void DecryptToConnectLevel(ProtocolServerBase server)
        {
            if (_rsa == null) return;
            DecryptToRamLevel(server);
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

        public void Database_UpdateServer(ProtocolServerBase org)
        {
            Debug.Assert(org.Id > 0);
            var tmp = (ProtocolServerBase)org.Clone();
            tmp.SetNotifyPropertyChangedEnabled(false);
            EncryptToDatabaseLevel(ref tmp);
            _db.UpdateServer(tmp);
        }

        public bool Database_DeleteServer(int id)
        {
            return _db.DeleteServer(id);
        }

        public List<ProtocolServerBase> Database_GetServers()
        {
            return _db.GetServers();
        }
    }
}