using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;

namespace _1RM.View.Editor.Forms
{
    public partial class BaseFormWithAddressPort : FormBase
    {
        public BaseFormWithAddressPort(ProtocolBase vm) : base(vm)
        {
            InitializeComponent();
        }
    }
}
