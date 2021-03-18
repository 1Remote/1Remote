using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PRM.Core.Model;
using PRM.Core.Protocol.RDP;
using Shawn.Utils;

namespace PRM.Core.Protocol.Putty.Telnet
{
    public class ProtocolServerTelnet : ProtocolServerWithAddrPortBase, IPuttyConnectable
    {
        public ProtocolServerTelnet() : base("Telnet", "Putty.Telnet.V1", "Telnet")
        {
        }

        public override bool IsOnlyOneInstance()
        {
            return false;
        }

        public override ProtocolServerBase CreateFromJsonString(string jsonString)
        {
            try
            {
                var ret = JsonConvert.DeserializeObject<ProtocolServerTelnet>(jsonString);
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

        public string GetPuttyConnString(PrmContext _)
        {
            return $@" -load ""{this.GetSessionName()}"" -telnet {Address} -P {Port}";
        }

        [JsonIgnore]
        public ProtocolServerBase ProtocolServerBase => this;

        private string _externalKittySessionConfigPath;

        public string ExternalKittySessionConfigPath
        {
            get => _externalKittySessionConfigPath;
            set => SetAndNotifyIfChanged(nameof(ExternalKittySessionConfigPath), ref _externalKittySessionConfigPath, value);
        }
    }
}