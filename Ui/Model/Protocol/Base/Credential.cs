using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Sockets;
using System.Reflection;
using _1Remote.Security;
using _1RM.Service.DataSource.DAO.Dapper;
using _1RM.Utils;
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
        public Credential(bool? isEditable = true)
        {
            IsEditable = isEditable;
        }

        /// <summary>
        /// 批量编辑时，如果参数列表不同，禁用
        /// </summary>
        [JsonIgnore] [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate, NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsEditable { get; }


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
        [OtherName(Name = "1RM_HOSTNAME")]
        public string Address
        {
            get => _address;
            set => SetAndNotifyIfChanged(ref _address, value);
        }

        private string _port = "";
        [OtherName(Name = "1RM_PORT")]
        public string Port
        {
            get => _port;
            set => SetAndNotifyIfChanged(ref _port, value);
        }

        private string _userName = "";
        [OtherName(Name = "1RM_USERNAME")]
        public string UserName
        {
            get => _userName;
            set => SetAndNotifyIfChanged(ref _userName, value);
        }

        private string _password = "";
        [OtherName(Name = "1RM_PASSWORD")]
        public string Password
        {
            get => _password;
            set => SetAndNotifyIfChanged(ref _password, value);
        }

        private string _privateKeyPath = "";

        [OtherName(Name = "1RM_PRIVATE_KEY_PATH")]
        public string PrivateKeyPath
        {
            get => _privateKeyPath;
            set => SetAndNotifyIfChanged(ref _privateKeyPath, value);
        }

        public static bool TestAddressPortIsAvailable(string address, string port, int timeOutMillisecond = 0)
        {
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
        public static bool TestAddressPortIsAvailable(ProtocolBaseWithAddressPort protocol, Credential credential, int timeOutMillisecond = 0)
        {
            if (string.IsNullOrEmpty(credential.Address) && string.IsNullOrEmpty(credential.Port))
            {
                return false;
            }
            string address = string.IsNullOrEmpty(credential.Address) ? protocol.Address : credential.Address;
            string port = string.IsNullOrEmpty(credential.Port) ? protocol.Port : credential.Port;
            return TestAddressPortIsAvailable(address, port, timeOutMillisecond);
        }

        public void Trim()
        {
            this.Name = Name.Trim();
            this.Address = Address.Trim();
            this.Port = Port.Trim();
            this.PrivateKeyPath = PrivateKeyPath.Trim();
        }

        public virtual bool SetCredential(in Credential credential)
        {
            Name = credential.Name;
            var a = SetAddress(credential);
            var b = SetPort(credential);
            var c = SetUserName(credential);
            var d = SetPassword(credential);
            var e = SetPrivateKeyPath(credential);
            return a || b || c || d || e;
        }

        public virtual bool SetAddress(in Credential credential)
        {
            if (!string.IsNullOrEmpty(credential.Address))
            {
                Address = credential.Address.Trim();
                return true;
            }
            return false;
        }
        public bool SetPort(in Credential credential)
        {
            if (!string.IsNullOrEmpty(credential.Port))
            {
                Port = credential.Port.Trim();
                return true;
            }
            return false;
        }

        public bool SetUserName(in Credential credential)
        {
            if (!string.IsNullOrEmpty(credential.UserName))
            {
                UserName = credential.UserName.Trim();
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
                Password = credential.PrivateKeyPath.Trim();
                return true;
            }
            return false;
        }

        public bool IsValueEqualTo(in Credential credential)
        {
            if (this.Name != credential.Name) return false;
            if (this.Address != credential.Address) return false;
            if (this.Port != credential.Port) return false;
            if (this.UserName != credential.UserName) return false;
            if (UnSafeStringEncipher.DecryptOrReturnOriginalString(Password) != UnSafeStringEncipher.DecryptOrReturnOriginalString(credential.Password)) return false;
            if (UnSafeStringEncipher.DecryptOrReturnOriginalString(PrivateKeyPath) != UnSafeStringEncipher.DecryptOrReturnOriginalString(credential.PrivateKeyPath)) return false;
            return true;
        }
    }
}
