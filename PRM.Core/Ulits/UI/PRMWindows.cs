using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace PRM.Core.Ulits.UI
{
    public class PRMWindows : Window
    {
        public PRMWindows()
        {
            Loaded += (sender, evnt) =>
            {
                var MinimizeButton = (Button)Template.FindName("PART_MinimizeButton", this);
                var MaximizeButton = (Button)Template.FindName("PART_MaximizeButton", this);
                var CloseButton = (Button)Template.FindName("PART_CloseButton", this);
                var SystemMenuButton = (Button)Template.FindName("PART_SystemMenuButton", this);

                MinimizeButton.Click += (s, e) => WindowState = WindowState.Minimized;
                MaximizeButton.Click += (s, e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                CloseButton.Click += (s, e) => Close();
                //SystemMenuButton.Click += (s, e) => SystemCommands.ShowSystemMenu(this, GetMousePosition());
            };
        }
    }
}
