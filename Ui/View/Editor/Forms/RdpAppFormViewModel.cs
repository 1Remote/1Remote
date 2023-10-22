using _1RM.Model.Protocol;

namespace _1RM.View.Editor.Forms
{
    public class RdpAppFormViewModel : ProtocolBaseWithAddressPortUserPwdFormViewModel
    {
        public new RdpApp New { get; }
        public RdpAppFormViewModel(RdpApp protocolBase) : base(protocolBase)
        {
            New = protocolBase;
        }
    }
}
