using System.Collections.ObjectModel;
using Shawn.Utils;

namespace _1RM.Model.Protocol.Base
{
    public abstract class ProtocolBaseWithAddressPort : ProtocolBase
    {
        protected ProtocolBaseWithAddressPort(string protocol, string classVersion, string protocolDisplayName, string protocolDisplayNameInShort = "") : base(protocol, classVersion, protocolDisplayName, protocolDisplayNameInShort)
        {
        }

        #region Conn

        private string _address = "";
        [OtherName(Name = "HOSTNAME")]
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

        [OtherName(Name = "PORT")]
        public string Port
        {
            get => _port;
            set => SetAndNotifyIfChanged(ref _port, value);
        }

        protected override string GetSubTitle()
        {
            return $"{Address}:{Port}";
        }

        #endregion Conn
    }
}