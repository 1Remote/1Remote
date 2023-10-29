using _1RM.Model.Protocol;

namespace _1RM.View.Editor.Forms
{
    public class SshFormViewModel : ProtocolBaseWithAddressPortUserPwdFormViewModel
    {
        public new SSH New { get; }
        public SshFormViewModel(SSH protocolBase) : base(protocolBase)
        {
            New = protocolBase;
        }
    }
}
