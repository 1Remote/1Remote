using PRM.Core.Protocol;

namespace PRM.View.ProtocolEditors
{
    public partial class TelnetForm : ProtocolServerFormBase
    {
        public readonly ProtocolServerBase Vm;
        public TelnetForm(ProtocolServerBase vm) : base(vm)
        {
            InitializeComponent();
            Vm = vm;
            DataContext = vm;
        }
    }
}
