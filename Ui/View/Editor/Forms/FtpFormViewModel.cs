using _1RM.Model.Protocol;

namespace _1RM.View.Editor.Forms
{
    public class FtpFormViewModel : ProtocolBaseWithAddressPortUserPwdFormViewModel
    {
        public new FTP New { get; }
        public FtpFormViewModel(FTP protocolBase) : base(protocolBase)
        {
            New = protocolBase;
        }
    }
}
