using PRM.Model.Protocol;
using PRM.Model.Protocol.Base;

namespace PRM.View.Editor.Forms
{
    public partial class RdpAppForm : FormBase
    {
        public RdpApp Vm;
        public RdpAppForm(ProtocolBase vm) : base(vm)
        {
            InitializeComponent();
            Vm = (RdpApp)vm;
            DataContext = vm;
        }
    }
}
