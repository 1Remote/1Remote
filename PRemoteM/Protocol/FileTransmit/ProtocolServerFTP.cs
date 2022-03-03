using Newtonsoft.Json;
using PRM.Model;
using PRM.Protocol.Base;
using PRM.Protocol.FileTransmit.Transmitters;
using Shawn.Utils;

namespace PRM.Protocol.FileTransmit
{
    public class ProtocolServerFTP : ProtocolServerWithAddrPortUserPwdBase, IProtocolFileTransmittable
    {
        public static string ProtocolName = "FTP";
        public ProtocolServerFTP() : base(ProtocolName, "FTP.V1", "FTP")
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

        public override ProtocolServerBase CreateFromJsonString(string jsonString)
        {
            try
            {
                var ret = JsonConvert.DeserializeObject<ProtocolServerFTP>(jsonString);
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

        public ITransmitter GeTransmitter(PrmContext context)
        {
            var hostname = this.Address;
            int port = this.GetPort();
            var username = this.UserName;
            var password = context.DataService.DecryptOrReturnOriginalString(this.Password);
            return new TransmitterFtp(hostname, port, username, password);
        }

        public string GetStartupPath()
        {
            return StartupPath;
        }
    }
}
