using _1RM.Model.Protocol.Base;
using _1RM.View.Editor.Forms.AlternativeCredential;

namespace _1RM.View.Editor.Forms
{
    public class ProtocolBaseWithAddressPortFormViewModel : ProtocolBaseFormViewModel
    {
        public new ProtocolBaseWithAddressPort New { get; }
        public AlternativeCredentialListViewModel AlternativeCredentialListViewModel { get; }
        public ProtocolBaseWithAddressPortFormViewModel(ProtocolBaseWithAddressPort protocolBase) : base(protocolBase)
        {
            New = protocolBase;
            AlternativeCredentialListViewModel = new AlternativeCredentialListViewModel(protocolBase);
        }
    }
}
