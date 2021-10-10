using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using JsonKnownTypes;
using Newtonsoft.Json;
using PRM.Core.Protocol.Runner.Default;
using PRM.Core.Service;

namespace PRM.Core.Protocol.Runner
{
    public class ExternalRunner : Runner
    {
        public ExternalRunner(string runnerName) : base(runnerName)
        {
        }

        protected string _exePath = "";
        public string ExePath
        {
            get => _exePath;
            set => SetAndNotifyIfChanged(ref _exePath, value);
        }


        protected string _arguments = "";
        public string Arguments
        {
            get => _arguments;
            set => SetAndNotifyIfChanged(ref _arguments, value);
        }
    }
}
