using PRM.Core.Protocol;
using PRM.Core.Protocol.Extend;

namespace PRM.View.ProtocolEditors
{
    public partial class AppForm : ProtocolServerFormBase
    {
        public readonly ProtocolServerBase Vm;
        public AppForm(ProtocolServerBase vm) : base(vm)
        {
            InitializeComponent();
            Vm = vm;
            DataContext = vm;
        }
        public override bool CanSave()
        {
            if (Vm is ProtocolServerApp app)
            {
                if (string.IsNullOrEmpty(app.ExePath) == false)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
