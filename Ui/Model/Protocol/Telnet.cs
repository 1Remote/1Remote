using System;
using Newtonsoft.Json;
using _1RM.Model.Protocol.Base;
using _1RM.Utils.PuTTY;
using _1RM.Utils.PuTTY;
using Shawn.Utils;

namespace _1RM.Model.Protocol
{
    public class Telnet : ProtocolBaseWithAddressPort, IPuttyConnectable
    {
        public static string ProtocolName = "Telnet";
        public Telnet() : base(Telnet.ProtocolName, "Putty.Telnet.V1", "Telnet")
        {
            base.Port = "23";
        }

        public override bool IsOnlyOneInstance()
        {
            return false;
        }

        public override ProtocolBase? CreateFromJsonString(string jsonString)
        {
            try
            {
                var ret = JsonConvert.DeserializeObject<Telnet>(jsonString);
                return ret;
            }
            catch (Exception e)
            {
                SimpleLogHelper.Debug(e);
                return null;
            }
        }

        public override double GetListOrder()
        {
            return 3;
        }

        private string _startupAutoCommand = "";

        public string StartupAutoCommand
        {
            get => _startupAutoCommand;
            set => SetAndNotifyIfChanged(ref _startupAutoCommand, value);
        }

        [JsonIgnore]
        public ProtocolBase ProtocolBase => this;

        private string _externalKittySessionConfigPath = "";
        public string ExternalKittySessionConfigPath
        {
            get => _externalKittySessionConfigPath;
            set => SetAndNotifyIfChanged(ref _externalKittySessionConfigPath, value);
        }



        private string _externalSessionConfigPath = "";
        public string ExternalSessionConfigPath
        {
            get => string.IsNullOrEmpty(_externalSessionConfigPath) ? _externalKittySessionConfigPath : _externalSessionConfigPath;
            set => SetAndNotifyIfChanged(ref _externalSessionConfigPath, value);
        }
    }
}