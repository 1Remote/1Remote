using PRM.Model.Protocol;
using PRM.Model.Protocol.Base;

namespace PRM.View.Editor.Forms
{
    public partial class TelnetForm : FormBase
    {
        public readonly ProtocolBase Vm;
        public TelnetForm(ProtocolBase vm) : base(vm)
        {
            InitializeComponent();
            Vm = vm;
            DataContext = vm;
        }
    }
}
