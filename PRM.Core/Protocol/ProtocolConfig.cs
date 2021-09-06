using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PRM.Core.Protocol.Runner;

namespace PRM.Core.Protocol
{
    public class ProtocolConfig
    {
        public string ProtocolName { get; }

        public ProtocolRunner DefaultExternalRunner;

        public List<ProtocolRunner> Runners;

        public ProtocolConfig(string protocolName)
        {
            ProtocolName = protocolName;
        }
    }
}
