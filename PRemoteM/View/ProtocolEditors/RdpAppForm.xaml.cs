using PRM.Protocol.Base;
using PRM.Protocol.RDP;

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
