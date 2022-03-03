using System;
using Newtonsoft.Json;
using PRM.Model;
using PRM.Protocol.Base;
using PRM.Utils.KiTTY;
using Shawn.Utils;

namespace PRM.Protocol.Putty.Telnet
{
    public class ProtocolServerTelnet : ProtocolServerWithAddrPortBase, IKittyConnectable
    {
        public static string ProtocolName = "Telnet";
        public ProtocolServerTelnet() : base(ProtocolServerTelnet.ProtocolName, "Putty.Telnet.V1", "Telnet")
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

        private string _startupAutoCommand = "";

        public string StartupAutoCommand
        {
            get => _startupAutoCommand;
            set => SetAndNotifyIfChanged(ref _startupAutoCommand, value);
        }

        [JsonIgnore]
        public ProtocolServerBase ProtocolServerBase => this;

        private string _externalKittySessionConfigPath;

        public string ExternalKittySessionConfigPath
        {
            get => _externalKittySessionConfigPath;
            set => SetAndNotifyIfChanged(ref _externalKittySessionConfigPath, value);
        }

        public string GetExeFullPath()
        {
            return this.GetKittyExeFullName();
        }

        public string GetExeArguments(PrmContext context)
        {
            return GetPuttyConnString(context);
        }
    }
}