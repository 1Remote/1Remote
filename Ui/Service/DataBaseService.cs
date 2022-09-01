using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using com.github.xiangyuecn.rsacsharp;
using _1RM.Model.DAO;
using _1RM.Model.DAO.Dapper;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;

namespace _1RM.Service
{

    public static class DataService
    {
        public static void EncryptToDatabaseLevel(this RSA? rsa, ref ProtocolBase server)
        {
            if (rsa == null) return;
            // ! server must be decrypted
            server.DisplayName = Encrypt(rsa, server.DisplayName);

            // encrypt some info
            if (server.GetType().IsSubclassOf(typeof(ProtocolBaseWithAddressPort)))
            {
                var p = (ProtocolBaseWithAddressPort)server;
                p.Address = Encrypt(rsa, p.Address);
                p.SetPort(Encrypt(rsa, p.Port));
            }
            if (server.GetType().IsSubclassOf(typeof(ProtocolBaseWithAddressPortUserPwd)))
            {
                var p = (ProtocolBaseWithAddressPortUserPwd)server;
                p.UserName = Encrypt(rsa, p.UserName);
            }


            // encrypt password
            if (server.GetType().IsSubclassOf(typeof(ProtocolBaseWithAddressPortUserPwd)))
            {
                var s = (ProtocolBaseWithAddressPortUserPwd)server;
                s.Password = Encrypt(rsa, s.Password);
            }
            switch (server)
            {
                case SSH ssh when !string.IsNullOrWhiteSpace(ssh.PrivateKey):
                {
                    ssh.PrivateKey = Encrypt(rsa, ssh.PrivateKey);
                    break;
                }
                case RDP rdp when !string.IsNullOrWhiteSpace(rdp.GatewayPassword):
                {
                    rdp.GatewayPassword = Encrypt(rsa, rdp.GatewayPassword);
                    break;
                }
            }
        }
        private static string Encrypt(this RSA? rsa, string str)
        {
            if (rsa?.DecodeOrNull(str) == null)
                return rsa?.Encode(str) ?? str;
            return str;
        }


        public static string DecryptOrReturnOriginalString(this RSA? ras, string originalString)
        {
            return ras?.DecodeOrNull(originalString) ?? originalString;
        }


        public static void DecryptToConnectLevel(this RSA? rsa, ref ProtocolBase server)
        {
            if (rsa == null) return;
            DecryptToRamLevel(rsa, ref server);
            if (server.GetType().IsSubclassOf(typeof(ProtocolBaseWithAddressPortUserPwd)))
            {
                var s = (ProtocolBaseWithAddressPortUserPwd)server;
                s.Password = DecryptOrReturnOriginalString(rsa, s.Password);
            }
            switch (server)
            {
                case SSH ssh when !string.IsNullOrWhiteSpace(ssh.PrivateKey):
                    ssh.PrivateKey = DecryptOrReturnOriginalString(rsa, ssh.PrivateKey);
                    break;

                case RDP rdp when !string.IsNullOrWhiteSpace(rdp.GatewayPassword):
                    rdp.GatewayPassword = DecryptOrReturnOriginalString(rsa, rdp.GatewayPassword);
                    break;
            }
        }

        public static void DecryptToRamLevel(this RSA? rsa, ref ProtocolBase server)
        {
            if (rsa == null) return;
            server.DisplayName = DecryptOrReturnOriginalString(rsa, server.DisplayName);
            if (server.GetType().IsSubclassOf(typeof(ProtocolBaseWithAddressPort)))
            {
                var p = (ProtocolBaseWithAddressPort)server;
                p.Address = DecryptOrReturnOriginalString(rsa, p.Address);
                p.Port = DecryptOrReturnOriginalString(rsa, p.Port);
            }

            if (server.GetType().IsSubclassOf(typeof(ProtocolBaseWithAddressPortUserPwd)))
            {
                var p = (ProtocolBaseWithAddressPortUserPwd)server;
                p.UserName = DecryptOrReturnOriginalString(rsa, p.UserName);
            }
        }
    }
}