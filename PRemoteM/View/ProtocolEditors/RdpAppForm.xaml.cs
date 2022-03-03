using PRM.Model.Protocol.Base;
using PRM.Model.Protocol.RDP;

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
