using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Dragablz;
using PRM.Core.Model;
using PRM.Model;
using PRM.ViewModel;
using Shawn.Utils;
using Shawn.Utils.DragablzTab;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Size = System.Windows.Size;

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
