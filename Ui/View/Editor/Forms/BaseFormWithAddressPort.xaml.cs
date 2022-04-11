using PRM.Model.Protocol;
using PRM.Model.Protocol.Base;

namespace PRM.View.Editor.Forms
{
    public partial class BaseFormWithAddressPort : FormBase
    {
        public BaseFormWithAddressPort(ProtocolBase vm) : base(vm)
        {
            InitializeComponent();
        }
    }
}
