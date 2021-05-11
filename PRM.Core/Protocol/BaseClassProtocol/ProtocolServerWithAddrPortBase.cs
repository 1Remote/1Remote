using System;

namespace PRM.Core.Protocol
{
    public abstract class ProtocolServerWithAddrPortBase : ProtocolServerBase
    {
        protected ProtocolServerWithAddrPortBase(string protocol, string classVersion, string protocolDisplayName, string protocolDisplayNameInShort = "") : base(protocol, classVersion, protocolDisplayName, protocolDisplayNameInShort)
        {
        }

        #region Conn

        private string _address = "";

        public string Address
        {
            get => _address;
            set => SetAndNotifyIfChanged(nameof(Address), ref _address, value);
        }

        public int GetPort()
        {
            if (int.TryParse(Port, out var p))
                return p;
            return 0;
        }

        private string _port = "3389";

        public string Port
        {
            get => _port;
            set => SetAndNotifyIfChanged(nameof(Port), ref _port, value);
        }

        protected override string GetSubTitle()
        {
            return $"@{Address}:{Port}";
        }

        #endregion Conn
    }
}