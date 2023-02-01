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

        private void ButtonMaximize_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (WindowState == WindowState.Normal)
            {
                WindowState = WindowState.Maximized;
            }
            else
            {
                WindowStyle = WindowStyle.SingleBorderWindow;
                ResizeMode = ResizeMode.CanResize;
                WindowState = WindowState.Normal;
            }
        }

        private void ButtonMaximize_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (WindowState == WindowState.Normal)
            {
                WindowStyle = WindowStyle.None;
                ResizeMode = ResizeMode.NoResize;
                WindowState = WindowState.Maximized;
            }
            else
            {
                WindowStyle = WindowStyle.SingleBorderWindow;
                ResizeMode = ResizeMode.CanResize;
                WindowState = WindowState.Normal;
            }
        }
    }
}