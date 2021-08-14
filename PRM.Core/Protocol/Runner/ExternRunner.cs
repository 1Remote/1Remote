using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PRM.Core.Protocol.Runner
{
    public class ExternRunner : Runner
    {
        public ExternRunner(string protocol) : base(protocol)
        {
        }

        private string _name = "";
        public string Name
        {
            get => _name;
            set => SetAndNotifyIfChanged(nameof(Name), ref _name, value);
        }

        private string _exePath = "";
        public string ExePath
        {
            get => _exePath;
            set => SetAndNotifyIfChanged(nameof(ExePath), ref _exePath, value);
        }


        private string _arguments = "";
        public string Arguments
        {
            get => _arguments;
            set => SetAndNotifyIfChanged(nameof(Arguments), ref _arguments, value);
        }
    }
}
