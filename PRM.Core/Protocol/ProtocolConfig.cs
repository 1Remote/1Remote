using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PRM.Core.Protocol.Runner;

namespace PRM.Core.Protocol
{
    public class ProtocolConfig
    {
        public string SelectedRunnerName { get; set; }

        public List<Runner.Runner> Runners;

        public Runner.Runner GetRunner()
        {
            if (Runners.Any(x => x.Name == SelectedRunnerName))
            {
                return Runners.First(x => x.Name == SelectedRunnerName);
            }
            return Runners.FirstOrDefault();
        }

        public ProtocolConfig()
        {
            Runners = new List<Runner.Runner>();
        }
    }
}
