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

        public override bool CanSave()
        {
            if (!string.IsNullOrEmpty(New[nameof(New.RemoteApplicationName)]))
                return false;
            if (!string.IsNullOrEmpty(New[nameof(New.RemoteApplicationProgram)]))
                return false;
            return base.CanSave();
        }
    }
}
