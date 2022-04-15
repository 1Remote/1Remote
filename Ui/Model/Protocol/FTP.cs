using Newtonsoft.Json;
using PRM.Model.Protocol.Base;
using PRM.Model.Protocol.FileTransmit;
using PRM.Model.Protocol.FileTransmit.Transmitters;
using PRM.Service;
using Shawn.Utils;

namespace PRM.Model.Protocol
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
        [OtherName(Name = "PRM_STARTUP_PATH")]
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
