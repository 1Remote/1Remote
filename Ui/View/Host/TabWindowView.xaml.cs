using System.Windows;
using System.Windows.Input;
using _1RM.Service;

namespace _1RM.View.Host
{
    public partial class TabWindowView : TabWindowBase
    {
        public TabWindowView(string token, LocalityService localityService) : base(token, localityService)
        {
            InitializeComponent();
            this.Loaded += (sender, args) =>
            {
                base.SetTabablzControl(TabablzControl);
            };
        }
    }
}