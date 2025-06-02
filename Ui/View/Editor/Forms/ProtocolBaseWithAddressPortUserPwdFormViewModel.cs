using _1RM.Model.Protocol.Base;
using _1RM.View.Editor.Forms.AlternativeCredential;

namespace _1RM.View.Editor.Forms
{
    public class ProtocolBaseWithAddressPortUserPwdFormViewModel : ProtocolBaseWithAddressPortFormViewModel
    {
        public new ProtocolBaseWithAddressPortUserPwd New { get; }
        public CredentialViewModel CredentialViewModel { get; }


        public ProtocolBaseWithAddressPortUserPwdFormViewModel(ProtocolBaseWithAddressPortUserPwd protocol) : base(protocol)
        {
            New = protocol;
            CredentialViewModel = new CredentialViewModel(protocol);
        }
    }
}
