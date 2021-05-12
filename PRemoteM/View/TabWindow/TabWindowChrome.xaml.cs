using System.Windows.Input;

namespace PRM.View.TabWindow
{
    public partial class TabWindowChrome : TabWindowBase
    {
        public TabWindowChrome(string token) : base(token)
        {
            InitializeComponent();
            base.Init(TabablzControl);
        }
    }
}