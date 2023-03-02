using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using com.github.xiangyuecn.rsacsharp;
using _1RM.Model.DAO;
using _1RM.Model.DAO.Dapper;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Utils;

namespace _1RM.Service
{

    public static class DataService
    {
        public static void EncryptToDatabaseLevel(this ProtocolBase server)
        {
            // encrypt password
            if (server.GetType().IsSubclassOf(typeof(ProtocolBaseWithAddressPortUserPwd)))
            {
                var s = (ProtocolBaseWithAddressPortUserPwd)server;
                s.Password = UnSafeStringEncipher.EncryptOnce(s.Password);
                foreach (var credential in s.AlternateCredentials)
                {
                    credential.Password = UnSafeStringEncipher.EncryptOnce(credential.Password);
                }
            }
            switch (server)
            {
                case SSH ssh when !string.IsNullOrWhiteSpace(ssh.PrivateKey):
                {
                    ssh.PrivateKey = UnSafeStringEncipher.EncryptOnce(ssh.PrivateKey);
                    break;
                }
                case RDP rdp when !string.IsNullOrWhiteSpace(rdp.GatewayPassword):
                {
                    rdp.GatewayPassword = UnSafeStringEncipher.EncryptOnce(rdp.GatewayPassword);
                    break;
                }
            }
        }

        public static void DecryptToConnectLevel(this ProtocolBase server)
        {
            if (server.GetType().IsSubclassOf(typeof(ProtocolBaseWithAddressPortUserPwd)))
            {
                var s = (ProtocolBaseWithAddressPortUserPwd)server;
                s.Password = UnSafeStringEncipher.DecryptOrReturnOriginalString(s.Password);

                foreach (var credential in s.AlternateCredentials)
                {
                    credential.Password = UnSafeStringEncipher.DecryptOrReturnOriginalString(credential.Password);
                }
            }
            switch (server)
            {
                case SSH ssh when !string.IsNullOrWhiteSpace(ssh.PrivateKey):
                    ssh.PrivateKey = UnSafeStringEncipher.DecryptOrReturnOriginalString(ssh.PrivateKey);
                    break;

                case RDP rdp when !string.IsNullOrWhiteSpace(rdp.GatewayPassword):
                    rdp.GatewayPassword = UnSafeStringEncipher.DecryptOrReturnOriginalString(rdp.GatewayPassword);
                    break;
            }
        }
    }
}