using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PRM.Core.Protocol.Runner
{
    public abstract class Runner : NotifyPropertyChangedBase
    {
        protected Runner(string protocol)
        {
            Protocol = protocol;
        }

        public string Protocol { get; }
    }
}
