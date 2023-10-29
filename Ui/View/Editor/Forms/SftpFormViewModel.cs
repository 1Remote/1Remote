using _1RM.Model.Protocol;

namespace _1RM.View.Editor.Forms
{
    public class SftpFormViewModel : ProtocolBaseWithAddressPortUserPwdFormViewModel
    {
        public new SFTP New { get; }
        public SftpFormViewModel(SFTP protocolBase) : base(protocolBase)
        {
            New = protocolBase;
        }
    }
}
