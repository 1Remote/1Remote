using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;

namespace _1RM.View.Editor.Forms
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
