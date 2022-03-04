using PRM.Model.Protocol;
using PRM.Model.Protocol.Base;

namespace PRM.View.Editor.Forms
{
    public partial class AppForm : FormBase
    {
        public readonly ProtocolBase Vm;
        public AppForm(ProtocolBase vm) : base(vm)
        {
            InitializeComponent();
            Vm = vm;
            DataContext = vm;
        }
        public override bool CanSave()
        {
            if (Vm is LocalApp app)
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
