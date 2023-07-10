using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Newtonsoft.Json;
using _1RM.Model.Protocol;
using _1RM.Service;
using Shawn.Utils;

namespace _1RM.Model.ProtocolRunner
{
    public class ExternalRunnerForSSH : ExternalRunner
    {
        public ExternalRunnerForSSH(string runnerName, string ownerProtocolName) : base(runnerName, ownerProtocolName)
        {
        }


        public string ArgumentsForPrivateKey
        {
            get => Params.ContainsKey(nameof(ArgumentsForPrivateKey)) ? Params[nameof(ArgumentsForPrivateKey)] : "";
            set
            {
                if (Params.ContainsKey(nameof(ArgumentsForPrivateKey)) == false)
                {
                    Params.Add(nameof(ArgumentsForPrivateKey), value);
                    RaisePropertyChanged();

                }
                else if (Params.ContainsKey(nameof(ArgumentsForPrivateKey)) && Params[nameof(ArgumentsForPrivateKey)] != value)
                {
                    Params[nameof(ArgumentsForPrivateKey)] = value;
                    RaisePropertyChanged();
                }
            }
        }
    }
}
