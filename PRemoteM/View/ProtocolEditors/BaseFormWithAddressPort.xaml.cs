using PRM.Protocol.Base;

namespace PRM.View.ProtocolEditors
{
    public partial class BaseFormWithAddressPort : ProtocolServerFormBase
    {
        public readonly ProtocolServerBase Vm;
        public BaseFormWithAddressPort(ProtocolServerBase vm) : base(vm)
        {
            InitializeComponent();
            Vm = vm;
            DataContext = vm;
        }
    }
}
