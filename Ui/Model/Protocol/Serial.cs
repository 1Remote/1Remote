using System;
using System.Linq;
using Newtonsoft.Json;
using _1RM.Model.Protocol.Base;
using _1RM.Utils.KiTTY;
using Shawn.Utils;
using System.Collections.Generic;

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


        public string[] DataBitsCollection => new[] { "5", "6", "7", "8" };
        private string _dataBits = "8";
        public string DataBits
        {
            get => _dataBits;
            set => SetAndNotifyIfChanged(ref _dataBits, value);
        }


        public string[] StopBitsCollection => new[] { "1", "2" };
        private string _stopBits = "1";
        public string StopBits
        {
            get => _stopBits;
            set => SetAndNotifyIfChanged(ref _stopBits, value);
        }

        public string[] ParityCollection => new[] { "NONE", "ODD", "EVEN", "MARK", "SPACE" };
        private string _parity = "NONE";
        public string Parity
        {
            get => _parity;
            set => SetAndNotifyIfChanged(ref _parity, value);
        }

        public string GetParityFlag()
        {
            if (string.IsNullOrWhiteSpace(_parity))
                return "N";
            // ‘n’ for none, ‘o’ for odd, ‘e’ for even, ‘m’ for mark and ‘s’ for space.
            //string[] ParityFlagCollection = new[] { "n", "o", "e", "m", "s" };
            return _parity[0].ToString().ToUpper();
        }


        public string[] FlowControlCollection => new[] { "NONE", "XON/XOFF", "RTS/CTS", "DSR/DTR" };
        private string _flowControl = "XON/XOFF";
        public string FlowControl
        {
            get => _flowControl;
            set => SetAndNotifyIfChanged(ref _flowControl, value);
        }

        public string GetFlowControlFlag()
        {
            // ‘N’ for none, ‘X’ for XON/XOFF, ‘R’ for RTS/CTS and ‘D’ for DSR/DTR.
            if (string.IsNullOrWhiteSpace(_flowControl))
                return "N";
            return _flowControl[0].ToString().ToUpper();
        }

        private string _startupAutoCommand = "";
        public string StartupAutoCommand
        {
            get => _startupAutoCommand;
            set => SetAndNotifyIfChanged(ref _startupAutoCommand, value);
        }

        [JsonIgnore]
        public ProtocolBase ProtocolBase => this;

        [JsonIgnore]
        public List<string> SerialPorts => System.IO.Ports.SerialPort.GetPortNames().ToList();

        private string _externalKittySessionConfigPath = "";
        public string ExternalKittySessionConfigPath
        {
            get => _externalKittySessionConfigPath;
            set => SetAndNotifyIfChanged(ref _externalKittySessionConfigPath, value);
        }

        public string GetExeArguments(string sessionName)
        {
            // https://stackoverflow.com/questions/35411927/putty-command-line-automate-serial-commands-from-file
            // https://documentation.help/PuTTY/using-cmdline-sercfg.html
            // Any single digit from 5 to 9 sets the number of data bits.
            // ‘1’, ‘1.5’ or ‘2’ sets the number of stop bits.
            // Any other numeric string is interpreted as a baud rate.
            // A single lower-case letter specifies the parity: ‘n’ for none, ‘o’ for odd, ‘e’ for even, ‘m’ for mark and ‘s’ for space.
            // A single upper-case letter specifies the flow control: ‘N’ for none, ‘X’ for XON/XOFF, ‘R’ for RTS/CTS and ‘D’ for DSR/DTR.
            // For example, ‘-sercfg 19200,8,n,1,N’ denotes a baud rate of 19200, 8 data bits, no parity, 1 stop bit and no flow control.
            var serial = (this.Clone() as Serial)!;
            serial.ConnectPreprocess();
            return $@" -load ""{sessionName}"" -serial {serial.SerialPort} -sercfg {serial.BitRate},{DataBits},{GetParityFlag()},{StopBits},{GetFlowControlFlag()}";
        }
    }
}