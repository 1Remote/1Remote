using PRM.Model.Protocol;
using PRM.Model.Protocol.Base;

namespace PRM.View.Editor.Forms
{
    public partial class AppForm : FormBase
    {
        public AppForm(ProtocolBase vm) : base(vm)
        {
            InitializeComponent();
        }
        public override bool CanSave()
        {
            if (_vm is LocalApp app)
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
