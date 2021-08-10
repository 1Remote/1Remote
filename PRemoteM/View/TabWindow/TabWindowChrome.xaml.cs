using System.Windows.Input;
using PRM.Core.Service;

namespace PRM.View.TabWindow
{
    public partial class TabWindowChrome : TabWindowBase
    {
        public TabWindowChrome(string token, LocalityService localityService) : base(token, localityService)
        {
            InitializeComponent();
            base.Init(TabablzControl);
        }
    }
}