using PRM.Core.Protocol.BaseClassForm;

namespace PRM.Core.Protocol.RDP
{
    public partial class ProtocolServerRemoteAppForm : ProtocolServerFormBase
    {
        public ProtocolServerRemoteApp Vm;
        public ProtocolServerRemoteAppForm(ProtocolServerBase vm) : base(vm)
        {
            InitializeComponent();
            Vm = (ProtocolServerRemoteApp)vm;
            DataContext = vm;
        }
    }
}
