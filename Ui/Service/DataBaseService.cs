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
        public static string EncryptOnce(string str)
        {
            if (UnSafeStringEncipher.SimpleDecrypt(str) == null)
                return UnSafeStringEncipher.SimpleEncrypt(str);
            return str;
        }
        public static string DecryptOrReturnOriginalString(string originalString)
        {
            return UnSafeStringEncipher.SimpleDecrypt(originalString) ?? originalString;
        }



        public static void EncryptToDatabaseLevel(this ProtocolBase server)
        {
            // encrypt password
            if (server.GetType().IsSubclassOf(typeof(ProtocolBaseWithAddressPortUserPwd)))
            {
                var s = (ProtocolBaseWithAddressPortUserPwd)server;
                s.Password = EncryptOnce(s.Password);

                if (s.Credentials != null)
                    foreach (var credential in s.Credentials)
                    {
                        credential.Password = EncryptOnce(credential.Password);
                    }
            }
            switch (server)
            {
                case SSH ssh when !string.IsNullOrWhiteSpace(ssh.PrivateKey):
                {
                    ssh.PrivateKey = EncryptOnce(ssh.PrivateKey);
                    break;
                }
                case RDP rdp when !string.IsNullOrWhiteSpace(rdp.GatewayPassword):
                {
                    rdp.GatewayPassword = EncryptOnce(rdp.GatewayPassword);
                    break;
                }
            }
        }

        public static void DecryptToConnectLevel(this ProtocolBase server)
        {
            if (server.GetType().IsSubclassOf(typeof(ProtocolBaseWithAddressPortUserPwd)))
            {
                var s = (ProtocolBaseWithAddressPortUserPwd)server;
                s.Password = DecryptOrReturnOriginalString(s.Password);

                if (s.Credentials != null)
                    foreach (var credential in s.Credentials)
                    {
                        credential.Password = DecryptOrReturnOriginalString(credential.Password);
                    }
            }
            switch (server)
            {
                case SSH ssh when !string.IsNullOrWhiteSpace(ssh.PrivateKey):
                    ssh.PrivateKey = DecryptOrReturnOriginalString(ssh.PrivateKey);
                    break;

                case RDP rdp when !string.IsNullOrWhiteSpace(rdp.GatewayPassword):
                    rdp.GatewayPassword = DecryptOrReturnOriginalString(rdp.GatewayPassword);
                    break;
            }
        }
    }
}