using System;
using System.Net.Sockets;
using JsonKnownTypes;
using Newtonsoft.Json;
using Shawn.Utils;

namespace _1RM.Model.Protocol.Base
{
    [JsonConverter(typeof(JsonKnownTypesConverter<Credential>))] // json serialize/deserialize derived types https://stackoverflow.com/a/60296886/8629624
    //[JsonKnownType(typeof(ExternalRunner), nameof(ExternalRunner))]
    //[JsonKnownType(typeof(KittyRunner), nameof(KittyRunner))]
    //[JsonKnownType(typeof(InternalDefaultRunner), nameof(InternalDefaultRunner))]
    public class Credential : NotifyPropertyChangedBase, ICloneable
    {
        public object Clone()
        {
            return MemberwiseClone();
        }
        public Credential CloneMe()
        {
            return (MemberwiseClone() as Credential)!;
        }

        private string _name = "";
        public string Name
        {
            get => _name;
            set => SetAndNotifyIfChanged(ref _name, value);
        }


        private string _address = "";
        [OtherName(Name = "HOSTNAME")]
        public string Address
        {
            get => _address;
            set => SetAndNotifyIfChanged(ref _address, value);
        }

        private string _port = "";
        [OtherName(Name = "PORT")]
        public string Port
        {
            get => _port;
            set => SetAndNotifyIfChanged(ref _port, value);
        }

        private string _userName = "";
        [OtherName(Name = "USERNAME")]
        public string UserName
        {
            get => _userName;
            set => SetAndNotifyIfChanged(ref _userName, value);
        }

        private string _password = "";
        [OtherName(Name = "PASSWORD")]
        public string Password
        {
            get => _password;
            set => SetAndNotifyIfChanged(ref _password, value);
        }

        private string _privateKeyPath = "";
        [OtherName(Name = "PRIVATE_KEY_PATH")]
        public string PrivateKeyPath
        {
            get => _privateKeyPath;
            set => SetAndNotifyIfChanged(ref _privateKeyPath, value);
        }

        public static bool TestAddressPortIsAvailable(ProtocolBaseWithAddressPortUserPwd protocol, Credential credential, int timeOutMillisecond = 0)
        {
            if (string.IsNullOrEmpty(credential.Address) && string.IsNullOrEmpty(credential.Port))
            {
                return false;
            }

            string address = string.IsNullOrEmpty(credential.Address) ? protocol.Address : credential.Address;
            string port = string.IsNullOrEmpty(credential.Port) ? protocol.Port : credential.Port;
            try
            {
                var p = int.Parse(port);
                var client = new TcpClient();
                if (timeOutMillisecond > 0)
                {
                    if (!client.ConnectAsync(address, p).Wait(timeOutMillisecond))
                    {
                        throw new Exception();
                    }
                }
                else
                {
                    client.Connect(address, p);
                }
                client.Close();

                return true;
            }
            catch (Exception)
            {
                // ignored
            }
            return false;
        }



        public virtual bool SetCredential(in Credential credential)
        {
            var a = SetAddress(credential);
            var b = SetPort(credential);
            var c = SetUserName(credential);
            var d = SetPassword(credential);
            var e = SetPrivateKeyPath(credential);
            return a || b || c || d || e;
        }

        public virtual bool SetAddress(in Credential credential)
        {
            if (!string.IsNullOrEmpty(credential.Port))
            {
                Address = credential.Address;
                return true;
            }
            return false;
        }
        public bool SetPort(in Credential credential)
        {
            if (!string.IsNullOrEmpty(credential.Port))
            {
                Port = credential.Port;
                return true;
            }
            return false;
        }

        public bool SetUserName(in Credential credential)
        {
            if (!string.IsNullOrEmpty(credential.UserName))
            {
                UserName = credential.UserName;
                return true;
            }
            return false;
        }

        public bool SetPassword(in Credential credential)
        {
            if (!string.IsNullOrEmpty(credential.Password))
            {
                Password = credential.Password;
                return true;
            }
            return false;
        }

        public bool SetPrivateKeyPath(in Credential credential)
        {
            if (!string.IsNullOrEmpty(credential.PrivateKeyPath))
            {
                Password = credential.PrivateKeyPath;
                return true;
            }
            return false;
        }
    }
}
