using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

namespace PRM.View.TabWindow
{
    public interface ITab
    {
        Window GetWindow();

        VmTabWindow GetViewModel();

        Size GetTabContentSize();
    }
}
