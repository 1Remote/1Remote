using System;
using System.IO;
using System.Linq;
using JsonKnownTypes;
using Newtonsoft.Json;

namespace PRM.Core.Protocol.Runner.Default
{
    public class ExternalDefaultRunner : ExternalRunner
    {
        public ExternalDefaultRunner() : base("External runner")
        {
        }
    }
}
