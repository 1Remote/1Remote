﻿using System;
using System.Collections.Generic;
using System.Linq;
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
    }

    public class CredentialWithAddressPortUserPwd : Credential
    {
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

        public string GetAddress(ProtocolBaseWithAddressPortUserPwd protocol)
        {
            if (string.IsNullOrEmpty(Address))
                return protocol.Address;
            return Address;
        }

        public int GetPort(ProtocolBaseWithAddressPortUserPwd protocol)
        {
            if (string.IsNullOrEmpty(Port))
                return protocol.GetPort();
            if (int.TryParse(Port, out var p))
                return p;
            return 1;
        }

        public string GetUserName(ProtocolBaseWithAddressPortUserPwd protocol)
        {
            if (string.IsNullOrEmpty(UserName))
                return protocol.UserName;
            return UserName;
        }

        public string GetPassword(ProtocolBaseWithAddressPortUserPwd protocol)
        {
            if (string.IsNullOrEmpty(Password))
                return protocol.Password;
            return Password;
        }
    }
}
