using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using _1RM.Model;
using _1RM.Service;
using _1RM.Utils;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.FileSystem;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using UserControl = System.Windows.Controls.UserControl;

namespace _1RM.View.Settings
{
    public partial class SettingsPageView : UserControl
    {
        public SettingsPageView()
        {
            InitializeComponent();
        }
    }
}