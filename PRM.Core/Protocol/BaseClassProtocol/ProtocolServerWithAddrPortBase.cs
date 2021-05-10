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
            get => !string.IsNullOrEmpty(_port) ? _port : "3389";
            set
            {
                if (int.TryParse(value, out var p))
                {
                    if (p > 0 && p < 65536)
                    {
                        SetAndNotifyIfChanged(nameof(Port), ref _port, value);
                        return;
                    }
                }
            }
        }

        protected override string GetSubTitle()
        {
            return $"@{Address}:{Port}";
        }

        #endregion Conn
    }
}