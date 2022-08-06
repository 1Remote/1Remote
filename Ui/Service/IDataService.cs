using System.Collections.Generic;
using com.github.xiangyuecn.rsacsharp;
using _1RM.Model.DAO;
using _1RM.Model.Protocol.Base;

namespace _1RM.Service
{
    public interface IDataService
    {
        public IDataBase DB();

        public bool Database_OpenConnection(DatabaseType type, string newConnectionString);
        public void Database_CloseConnection();
        public EnumDbStatus Database_SelfCheck();
        public string Database_GetPublicKey();
        public string Database_GetPrivateKeyPath();
        //public RSA.EnumRsaStatus Database_SetEncryptionKey(string privateKeyPath, bool generateNewPublicKey = true);
        public RSA.EnumRsaStatus Database_SetEncryptionKey(string privateKeyPath, string privateKeyContent, IEnumerable<ProtocolBase> servers);
        public RSA.EnumRsaStatus Database_UpdatePrivateKeyPathOnly(string privateKeyPath);

        public string DecryptOrReturnOriginalString(string originalString);
        public void EncryptToDatabaseLevel(ref ProtocolBase server);
        public void DecryptToRamLevel(ref ProtocolBase server);
        public void DecryptToConnectLevel(ref ProtocolBase server);
        public void Database_InsertServer(ProtocolBase server);
        public void Database_InsertServer(IEnumerable<ProtocolBase> servers);
        public bool Database_UpdateServer(ProtocolBase org);
        public bool Database_UpdateServer(IEnumerable<ProtocolBase> servers);
        public bool Database_DeleteServer(string id);
        public bool Database_DeleteServer(IEnumerable<string> ids);
        public List<ProtocolBase> Database_GetServers();
    }
}
