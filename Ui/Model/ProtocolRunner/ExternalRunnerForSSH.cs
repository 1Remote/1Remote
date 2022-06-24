using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Newtonsoft.Json;
using PRM.Model.Protocol;
using Shawn.Utils;

namespace PRM.Model.ProtocolRunner
{
    public class ExternalRunnerForSSH : ExternalRunner
    {
        public ExternalRunnerForSSH(string runnerName, string ownerProtocolName) : base(runnerName, ownerProtocolName)
        {
        }


        private string _argumentsForPrivateKey = "";
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
