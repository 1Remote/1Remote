using Newtonsoft.Json;
using _1RM.Model.Protocol.Base;
using _1RM.Model.Protocol.FileTransmit;
using _1RM.Model.Protocol.FileTransmit.Transmitters;
using _1RM.Service;
using Shawn.Utils;

namespace _1RM.Model.Protocol
{
    // ReSharper disable once InconsistentNaming
    public class FTP : ProtocolBaseWithAddressPortUserPwd, IFileTransmittable
    {
        public static string ProtocolName = "FTP";
        public FTP() : base(ProtocolName, "FTP.V1", "FTP")
        {
            base.Port = "20";
        }

        private string _startupPath = "/";
        [OtherName(Name = "RM_STARTUP_PATH")]
        public string StartupPath
        {
            get => _startupPath;
            set => SetAndNotifyIfChanged(ref _startupPath, value);
        }

        public override bool IsOnlyOneInstance()
        {
            return false;
        }

        public override ProtocolBase? CreateFromJsonString(string jsonString)
        {
            try
            {
                var ret = JsonConvert.DeserializeObject<FTP>(jsonString);
                return ret;
            }
            catch
            {
                return null;
            }
        }

        public override double GetListOrder()
        {
            return 4;
        }

        public ITransmitter GeTransmitter()
        {
            var hostname = this.Address;
            int port = this.GetPort();
            var username = this.UserName;
            var password = IoC.Get<DataService>().DecryptOrReturnOriginalString(this.Password);
            return new TransmitterFtp(hostname, port, username, password);
        }

        public string GetStartupPath()
        {
            return StartupPath;
        }
    }
}
