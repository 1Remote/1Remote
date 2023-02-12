using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Shawn.Utils;

namespace _1RM.Model.Protocol.Base
{
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

                var iport = int.Parse(port);
                var client = new TcpClient();
                if (timeOutMillisecond > 0)
                {
                    if (!client.ConnectAsync(address, iport).Wait(timeOutMillisecond))
                    {
                        throw new Exception();
                    }
                }
                else
                {
                    client.Connect(address, iport);
                }
                client.Close();

                return true;
            }
            catch (Exception)
            {
                // ignored
            }

            SimpleLogHelper.Error($"TXT:{address}:{port} 未打开");

            return false;
        }



        public void SetCredential(in Credential credential)
        {
            if (!string.IsNullOrEmpty(credential.Address))
            {
                Address = credential.Address;
            }

            if (!string.IsNullOrEmpty(credential.Port))
            {
                Port = credential.Port;
            }

            if (!string.IsNullOrEmpty(credential.UserName))
            {
                UserName = credential.UserName;
            }

            if (!string.IsNullOrEmpty(credential.Password))
            {
                Password = credential.Password;
            }
        }
    }
}
