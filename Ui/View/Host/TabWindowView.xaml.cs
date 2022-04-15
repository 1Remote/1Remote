using PRM.Service;

namespace PRM.View.Host
{
    public partial class TabWindowView : TabWindowBase
    {
        public TabWindowView(string token, LocalityService localityService) : base(token, localityService)
        {
            var ws = localityService.TabWindowState;
            InitializeComponent();
            this.Loaded += (sender, args) =>
            {
                base.Init(TabablzControl);
                if (ws != System.Windows.WindowState.Minimized)
                {
                    this.WindowState = ws;
                }
            };
        }
    }
}