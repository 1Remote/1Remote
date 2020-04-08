using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace PRM.Core.Protocol
{
    public abstract class ProtocolServerFormBase : UserControl
    {
        public abstract bool CanSave();
    }
}
