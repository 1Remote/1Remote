using System;
using System.IO;
using System.Linq;
using JsonKnownTypes;
using Newtonsoft.Json;

namespace PRM.Core.Protocol.Runner.Default
{
    public class InternalDefaultRunner : Runner
    {
        public InternalDefaultRunner() : base(" Internal runner")
        {
        }
    }
}
