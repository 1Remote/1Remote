using PRM.Model.Protocol;
using PRM.Model.Protocol.Base;

namespace PRM.View.Editor.Forms
{
    public partial class TelnetForm : FormBase
    {
        public TelnetForm(ProtocolBase vm) : base(vm)
        {
            InitializeComponent();
        }
    }
}
