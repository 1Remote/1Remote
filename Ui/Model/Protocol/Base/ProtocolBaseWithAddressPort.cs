using System;
using System.Collections.ObjectModel;
using Newtonsoft.Json;
using System.ComponentModel;
using _1RM.Service;
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

        public const string MACRO_HOST_NAME = "%1RM_HOSTNAME%";
        private string _address = "";
        [OtherName(Name = "1RM_HOSTNAME")]
        public string Address
        {
            get => _address;
            set
            {
                var old = _address;
                if (SetAndNotifyIfChanged(ref _address, value))
                {
                    if (string.IsNullOrEmpty(DisplayName) || DisplayName == old)
                    {
                        DisplayName = value;
                    }
                    RaisePropertyChanged(nameof(SubTitle));
                }
            }
        }

        public int GetPort()
        {
            if (int.TryParse(Port, out var p))
                return p;
            return 1;
        }

        public const string MACRO_PORT = "%1RM_PORT%";
        private string _port = "3389";
        [OtherName(Name = "1RM_PORT")]
        public string Port
        {
            get => _port;
            set
            {
                if (SetAndNotifyIfChanged(ref _port, value))
                    RaisePropertyChanged(nameof(SubTitle));
            }
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


        /// <summary>
        /// return true if show address input
        /// </summary>
        public virtual bool ShowAddressInput()
        {
            return true;
        }

        /// <summary>
        /// return true if show port input
        /// </summary>
        public virtual bool ShowPortInput()
        {
            return true;
        }

        /// <summary>
        /// build the id for host
        /// </summary>
        /// <returns></returns>
        public override string BuildConnectionId()
        {
            return $"{Id}_{Address}:{Port}";
        }

        #region IDataErrorInfo
        [JsonIgnore]
        public override string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(Address):
                        {
                            if (this.ShowAddressInput() && string.IsNullOrWhiteSpace(Address))
                            {
                                return IoC.Translate(LanguageService.CAN_NOT_BE_EMPTY);
                            }
                            break;
                        }
                    case nameof(Port):
                        {
                            if (this.ShowPortInput())
                            {
                                if (string.IsNullOrWhiteSpace(Port))
                                    return IoC.Translate(LanguageService.CAN_NOT_BE_EMPTY);
                                if (!long.TryParse(Port, out _) && Port != ServerEditorDifferentOptions)
                                    return IoC.Translate("Not a number");
                            }
                            break;
                        }
                    default:
                        return base[columnName];
                }
                return "";
            }
        }
        #endregion
    }
}