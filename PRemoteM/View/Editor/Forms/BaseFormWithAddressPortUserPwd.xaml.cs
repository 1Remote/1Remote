using PRM.Model.Protocol;
using PRM.Model.Protocol.Base;

namespace PRM.View.Editor.Forms
{
    public partial class BaseFormWithAddressPortUserPwd : FormBase
    {
        public readonly ProtocolBase Vm;
        public BaseFormWithAddressPortUserPwd(ProtocolBase vm) : base(vm)
        {
            InitializeComponent();
            Vm = vm;
            DataContext = vm;
        }
    }
}
