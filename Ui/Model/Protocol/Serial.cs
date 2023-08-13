using System;
using Newtonsoft.Json;
using _1RM.Model.Protocol.Base;
using _1RM.Utils.KiTTY;
using Shawn.Utils;

namespace _1RM.Model.Protocol
{
    public class Serial : ProtocolBase, IKittyConnectable
    {
        public static string ProtocolName = "Serial";
        public Serial() : base(Serial.ProtocolName, "Putty.Serial.V1", "Serial")
        {
        }

        public override bool IsOnlyOneInstance()
        {
            return false;
        }

        public override ProtocolBase? CreateFromJsonString(string jsonString)
        {
            try
            {
                var ret = JsonConvert.DeserializeObject<Serial>(jsonString);
                return ret;
            }
            catch (Exception e)
            {
                SimpleLogHelper.Debug(e);
                return null;
            }
        }

        protected override string GetSubTitle()
        {
            return $"{SerialPort}({BitRate})";
        }

        public override double GetListOrder()
        {
            return 4;
        }


        private string _serialPort = "";
        public string SerialPort
        {
            get => _serialPort;
            set => SetAndNotifyIfChanged(ref _serialPort, value);
        }

        public int GetBitRate()
        {
            if (int.TryParse(BitRate, out var p))
                return p;
            return 9600;
        }

        public string[] BitRates = new[] { "9600", "14400", "38400", "56000", "57600", "115200", "128000", "256000" };

        private string _bitRate = "9600";
        public string BitRate
        {
            get => _bitRate;
            set => SetAndNotifyIfChanged(ref _bitRate, value);
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

        public string GetExeArguments(string sessionName)
        {
            // https://stackoverflow.com/questions/35411927/putty-command-line-automate-serial-commands-from-file
            var serial = (this.Clone() as Serial)!;
            serial.ConnectPreprocess();
            return $@" -load ""{sessionName}"" -serial {serial.SerialPort} -sercfg {serial.BitRate}";
        }
    }
}