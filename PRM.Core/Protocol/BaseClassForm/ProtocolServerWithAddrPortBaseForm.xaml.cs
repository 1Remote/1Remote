namespace PRM.Core.Protocol.BaseClassForm
{
    public partial class ProtocolServerWithAddrPortBaseForm : ProtocolServerFormBase
    {
        public readonly ProtocolServerBase Vm;
        public ProtocolServerWithAddrPortBaseForm(ProtocolServerBase vm) : base(vm)
        {
            InitializeComponent();
            Vm = vm;
            DataContext = vm;
        }
    }
}
