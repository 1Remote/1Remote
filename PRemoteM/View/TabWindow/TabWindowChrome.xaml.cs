using System.Windows.Input;
using PRM.Service;
using Shawn.Utils;

namespace PRM.View.TabWindow
{
    public partial class TabWindowChrome : TabWindowBase
    {
        public TabWindowChrome(string token, LocalityService localityService) : base(token, localityService)
        {
            var ws = localityService.TabWindowState;
            InitializeComponent();
            base.Init(TabablzControl);
            if (ws != System.Windows.WindowState.Minimized)
            {
                this.Loaded += (sender, args) =>
                {
                    this.WindowState = ws;
                };
            }
        }
    }
}