using _1RM.Model;
using _1RM.Utils;
using _1RM.Utils.Tracing;
using _1RM.View.ServerList;
using Stylet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Data;

namespace _1RM.View.ServerTree
{
    public partial class ServerTreeViewModel : ServerPageBase
    {
        public sealed override void CalcServerVisibleAndRefresh(bool force = false)
        {
            base.CalcServerVisibleAndRefresh(force);
            BuildView();
        }
    }
}