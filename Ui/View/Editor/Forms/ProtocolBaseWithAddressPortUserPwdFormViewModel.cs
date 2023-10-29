using _1RM.Model.Protocol.Base;
using Newtonsoft.Json;

namespace _1RM.View.Editor.Forms
{
    public class ProtocolBaseWithAddressPortUserPwdFormViewModel : ProtocolBaseWithAddressPortFormViewModel
    {
        public new ProtocolBaseWithAddressPortUserPwd New { get; }
        public ProtocolBaseWithAddressPortUserPwdFormViewModel(ProtocolBaseWithAddressPortUserPwd protocolBase) : base(protocolBase)
        {
            New = protocolBase;
        }
    }
}
