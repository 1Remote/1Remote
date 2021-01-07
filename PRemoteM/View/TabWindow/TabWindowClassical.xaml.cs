using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Dragablz;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Size = System.Windows.Size;
using PRM.Model;
using PRM.ViewModel;
using PRM.Core.Model;
using Shawn.Utils.DragablzTab;
using Shawn.Utils;

namespace PRM.View.TabWindow
{
    public partial class TabWindowClassical : TabWindowBase
    {
        public TabWindowClassical(string token) : base(token)
        {
            InitializeComponent();
            base.Init(TabablzControl);
        }

        protected override void TabablzControl_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Vm?.SelectedItem != null)
            {
                this.Icon =
                this.IconTitleBar.Source = Vm.SelectedItem.Content.ProtocolServer.IconImg;
            }
        }
    }
}
