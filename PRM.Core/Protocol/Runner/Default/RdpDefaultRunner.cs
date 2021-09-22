using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PRM.Core.Protocol.Runner.Default
{
    public class RdpDefaultRunner : Runner
    {
        public new static string Name = "Default";
        public RdpDefaultRunner() : base("RDP")
        {
            base.Name = Name;
        }
    }
}
