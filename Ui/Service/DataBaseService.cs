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
            if (server is ProtocolBaseWithAddressPortUserPwd s)
            {
                s.Password = UnSafeStringEncipher.EncryptOnce(s.Password);
                foreach (var credential in s.AlternateCredentials)
                {
                    credential.EncryptToDatabaseLevel();
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

                case LocalApp app:
                    foreach (var arg in app.ArgumentList)
                    {
                        if (arg.Type == AppArgumentType.Secret)
                        {
                            arg.Value = UnSafeStringEncipher.EncryptOnce(arg.Value);
                        }
                    }
                    break;
            }
        }

        public static void DecryptToConnectLevel(this ProtocolBase server)
        {
            if (server is ProtocolBaseWithAddressPortUserPwd s)
            {
                s.Password = UnSafeStringEncipher.DecryptOrReturnOriginalString(s.Password);
                foreach (var credential in s.AlternateCredentials)
                {
                    credential.DecryptToConnectLevel();
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

                case LocalApp app:
                    foreach (var arg in app.ArgumentList)
                    {
                        if (arg.Type == AppArgumentType.Secret)
                        {
                            arg.Value = UnSafeStringEncipher.DecryptOrReturnOriginalString(arg.Value);
                        }
                    }
                    break;
            }
        }


        public static void EncryptToDatabaseLevel(this Credential credential)
        {
            credential.Password = UnSafeStringEncipher.EncryptOnce(credential.Password);
            credential.PrivateKeyPath = UnSafeStringEncipher.EncryptOnce(credential.PrivateKeyPath);
        }

        public static void DecryptToConnectLevel(this Credential credential)
        {
            credential.Password = UnSafeStringEncipher.DecryptOrReturnOriginalString(credential.Password);
            credential.PrivateKeyPath = UnSafeStringEncipher.DecryptOrReturnOriginalString(credential.PrivateKeyPath);
        }
    }
}