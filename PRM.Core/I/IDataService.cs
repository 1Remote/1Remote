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
        public bool Database_OpenConnection(DatabaseType type, string newConnectionString);
        public void Database_CloseConnection();
        public bool Database_IsEncrypted();
        public EnumDbStatus Database_SelfCheck();
        public string Database_GetPublicKey();
        public string Database_GetPrivateKeyPath();
        public RSA.EnumRsaStatus Database_SetEncryptionKey(string privateKeyPath, bool generateNewPublicKey = true);
        public string DecryptOrReturnOriginalString(string originalString);
        public string Encrypt(string str);
        public void EncryptToDatabaseLevel(ProtocolServerBase server);
        public void DecryptToRamLevel(ProtocolServerBase server);
        public void DecryptToConnectLevel(ProtocolServerBase server);
        public void Database_InsertServer(ProtocolServerBase server);
        public void Database_UpdateServer(ProtocolServerBase org);
        public bool Database_DeleteServer(int id);
        public List<ProtocolServerBase> Database_GetServers();
    }
}
