using System.Collections.ObjectModel;
using Newtonsoft.Json;
using System.ComponentModel;
using Shawn.Utils;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace _1RM.Model.Protocol.Base
{
    public abstract class ProtocolBaseWithAddressPort : ProtocolBase
    {
        protected ProtocolBaseWithAddressPort(string protocol, string classVersion, string protocolDisplayName) : base(protocol, classVersion, protocolDisplayName)
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


        private ObservableCollection<Credential>? _alternateCredentials = new ObservableCollection<Credential>();
        public ObservableCollection<Credential> AlternateCredentials
        {
            get => _alternateCredentials ??= new ObservableCollection<Credential>();
            set => SetAndNotifyIfChanged(ref _alternateCredentials, value);
        }


        private bool? _isPingBeforeConnect = true;
        [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate, NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsPingBeforeConnect
        {
            get => _isPingBeforeConnect;
            set => SetAndNotifyIfChanged(ref _isPingBeforeConnect, value);
        }


        private bool? _isAutoAlternateAddressSwitching = true;
        [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate, NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsAutoAlternateAddressSwitching
        {
            get => _isAutoAlternateAddressSwitching;
            set => SetAndNotifyIfChanged(ref _isAutoAlternateAddressSwitching, value);
        }

        public virtual Credential GetCredential()
        {
            var c = new Credential()
            {
                Address = Address,
                Port = Port,
            };
            return c;
        }

        public virtual void SetCredential(in Credential credential)
        {
            if (!string.IsNullOrEmpty(credential.Address))
            {
                Address = credential.Address;
            }

            if (!string.IsNullOrEmpty(credential.Port))
            {
                Port = credential.Port;
            }
        }

        #endregion Conn
    }
}