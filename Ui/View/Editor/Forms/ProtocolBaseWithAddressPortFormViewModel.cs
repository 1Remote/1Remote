using _1RM.Model.Protocol.Base;
using _1RM.View.Editor.Forms.AlternativeCredential;
using _1RM.View.Editor.Forms.Utils;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace _1RM.View.Editor.Forms
{
    public class ProtocolBaseWithAddressPortFormViewModel : ProtocolBaseFormViewModel
    {
        public new ProtocolBaseWithAddressPort New { get; }
        public HostViewModel HostViewModel { get; }
        public AlternativeCredentialListViewModel AlternativeCredentialListViewModel { get; }
        public ProtocolBaseWithAddressPortFormViewModel(ProtocolBaseWithAddressPort protocolBase) : base(protocolBase)
        {
            New = protocolBase;
            HostViewModel = new HostViewModel(protocolBase);
            AlternativeCredentialListViewModel = new AlternativeCredentialListViewModel(protocolBase);
        }



        public override bool CanSave()
        {
            if (!string.IsNullOrEmpty(New[nameof(New.Address)]))
                return false;
            return base.CanSave();
        }
    }
}
