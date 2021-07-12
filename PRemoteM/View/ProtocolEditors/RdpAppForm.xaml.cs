using PRM.Core.Protocol;
using PRM.Core.Protocol.RDP;

namespace PRM.View.ProtocolEditors
{
    public partial class RdpAppForm : ProtocolServerFormBase
    {
        public ProtocolServerRemoteApp Vm;
        public RdpAppForm(ProtocolServerBase vm) : base(vm)
        {
            InitializeComponent();
            Vm = (ProtocolServerRemoteApp)vm;
            DataContext = vm;
        }
    }
}
