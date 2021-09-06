using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PRM.Core.Protocol.Runner
{
    public abstract class ProtocolRunner : NotifyPropertyChangedBase
    {
        protected ProtocolRunner(string protocol)
        {
            Protocol = protocol;
        }

        public string Protocol { get; }
    }
}
