namespace _1RM.Model.ProtocolRunner
{
    public class ExternalRunnerForSSH : ExternalRunner
    {
        public ExternalRunnerForSSH(string runnerName, string ownerProtocolName) : base(runnerName, ownerProtocolName)
        {
        }


        private string _argumentsForPrivateKey = "";
        public string ArgumentsForPrivateKey
        {
            get
            {
                if (string.IsNullOrEmpty(_argumentsForPrivateKey) && Params.ContainsKey(nameof(ArgumentsForPrivateKey)))
                {
                    _argumentsForPrivateKey = Params[nameof(ArgumentsForPrivateKey)];
                }
                return _argumentsForPrivateKey;
            }
            set
            {
                _argumentsForPrivateKey = value;
                if (Params.ContainsKey(nameof(ArgumentsForPrivateKey)) == false)
                {
                    Params.Add(nameof(ArgumentsForPrivateKey), value);

                }
                else if (Params.ContainsKey(nameof(ArgumentsForPrivateKey)) && Params[nameof(ArgumentsForPrivateKey)] != value)
                {
                    Params[nameof(ArgumentsForPrivateKey)] = value;
                }
                RaisePropertyChanged();
            }
        }
    }
}
