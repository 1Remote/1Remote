namespace PRM.Core.Protocol.BaseClassForm
{
    public partial class ProtocolServerWithAddrPortUserPwdBaseForm : ProtocolServerFormBase
    {
        public readonly ProtocolServerBase Vm;
        public ProtocolServerWithAddrPortUserPwdBaseForm(ProtocolServerBase vm) : base(vm)
        {
            InitializeComponent();
            Vm = vm;
            DataContext = vm;
        }
    }
}
