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
        /// <summary>
        /// validate whether all fields are correct to save
        /// </summary>
        /// <returns></returns>
        public abstract bool CanSave();
    }
}
