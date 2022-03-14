using System;
using PRM.Core.Model;
using Shawn.Utils;

namespace PRM.Core.Protocol
{
    public abstract class ProtocolServerWithAddrPortBase : ProtocolServerBase
    {
        protected ProtocolServerWithAddrPortBase(string protocol, string classVersion, string protocolDisplayName, string protocolDisplayNameInShort = "") : base(protocol, classVersion, protocolDisplayName, protocolDisplayNameInShort)
        {
        }

        #region Conn

        private string _address = "";

        [OtherName(Name = "PRM_HOSTNAME")]
        public string Address
        {
            get => _address;
            set => SetAndNotifyIfChanged(ref _address, value);
        }

        public int GetPort()
        {
            if (int.TryParse(Port, out var p))
                return p;
            return 1;
        }

        private string _port = "3389";

        [OtherName(Name = "PRM_PORT")]
        public string Port
        {
            get => _port;
            set => SetAndNotifyIfChanged(ref _port, value);
        }

        public void SetPort(string str)
        {
            _port = str;
        }

        protected override string GetSubTitle()
        {
            return $"{Address}:{Port}";
        }

        #endregion Conn
    }
}