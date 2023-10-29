using _1RM.Model.Protocol;

namespace _1RM.View.Editor.Forms
{
    public class VncFormViewModel : ProtocolBaseWithAddressPortUserPwdFormViewModel
    {
        public new VNC New { get; }
        public VncFormViewModel(VNC protocolBase) : base(protocolBase)
        {
            New = protocolBase;
        }
    }
}
