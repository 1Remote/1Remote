using PRM.Service;

namespace PRM.View.Host
{
    public partial class TabWindowView : TabWindowBase
    {
        public TabWindowView(string token, LocalityService localityService) : base(token, localityService)
        {
            InitializeComponent();
            this.Loaded += (sender, args) =>
            {
                base.Init(TabablzControl);
            };
        }
    }
}