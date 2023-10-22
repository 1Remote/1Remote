using _1RM.Model.Protocol;

namespace _1RM.View.Editor.Forms
{
    public class TelnetFormViewModel : ProtocolBaseWithAddressPortFormViewModel
    {
        public new Telnet New { get; }
        public TelnetFormViewModel(Telnet protocolBase) : base(protocolBase)
        {
            New = protocolBase;
        }
    }
}
