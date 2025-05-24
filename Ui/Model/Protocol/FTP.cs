using Newtonsoft.Json;
using _1RM.Model.Protocol.Base;
using _1RM.Model.Protocol.FileTransmit;
using _1RM.Model.Protocol.FileTransmit.Transmitters;
using Shawn.Utils;
using _1RM.Service;

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
        [OtherName(Name = "STARTUP_PATH")]
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
            return 6;
        }

        public ITransmitter GeTransmitter()
        {
            var ftp = (this.Clone() as FTP)!;
            ftp.DecryptToConnectLevel();
            return new TransmitterFtp(ftp.Address, ftp.GetPort(), ftp.UserName, ftp.Password);
        }

        public string GetStartupPath()
        {
            return StartupPath;
        }
    }
}
