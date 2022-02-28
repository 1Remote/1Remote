using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using com.github.xiangyuecn.rsacsharp;
using PRM.Core.DB;
using PRM.Core.Protocol;

namespace PRM.Core.I
{
    public interface IDataService
    {
        public IDb DB();

        public bool Database_OpenConnection(DatabaseType type, string newConnectionString);
        public void Database_CloseConnection();
        public EnumDbStatus Database_SelfCheck();
        public string Database_GetPublicKey();
        public string Database_GetPrivateKeyPath();
        //public RSA.EnumRsaStatus Database_SetEncryptionKey(string privateKeyPath, bool generateNewPublicKey = true);
        public RSA.EnumRsaStatus Database_SetEncryptionKey(string privateKeyPath, string privateKeyContent, IEnumerable<ProtocolServerBase> servers);
        public RSA.EnumRsaStatus Database_UpdatePrivateKeyPathOnly(string privateKeyPath);

        public string DecryptOrReturnOriginalString(string originalString);
        public string Encrypt(string str);
        public void EncryptToDatabaseLevel(ref ProtocolServerBase server);
        public void DecryptToRamLevel(ref ProtocolServerBase server);
        public void DecryptToConnectLevel(ref ProtocolServerBase server);
        public void Database_InsertServer(ProtocolServerBase server);
        public void Database_InsertServer(IEnumerable<ProtocolServerBase> servers);
        public bool Database_UpdateServer(ProtocolServerBase org);
        public bool Database_UpdateServer(IEnumerable<ProtocolServerBase> servers);
        public bool Database_DeleteServer(int id);
        public bool Database_DeleteServer(IEnumerable<int> ids);
        public List<ProtocolServerBase> Database_GetServers();
    }
}
