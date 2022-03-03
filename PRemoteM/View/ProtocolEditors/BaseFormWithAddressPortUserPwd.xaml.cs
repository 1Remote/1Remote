using PRM.Model.Protocol.Base;

namespace PRM.View.ProtocolEditors
{
    public partial class BaseFormWithAddressPortUserPwd : ProtocolServerFormBase
    {
        public readonly ProtocolServerBase Vm;
        public BaseFormWithAddressPortUserPwd(ProtocolServerBase vm) : base(vm)
        {
            InitializeComponent();
            Vm = vm;
            DataContext = vm;
        }
    }
}
