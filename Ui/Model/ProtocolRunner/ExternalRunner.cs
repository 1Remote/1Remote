using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using Newtonsoft.Json;
using Shawn.Utils;
using Shawn.Utils.Wpf;

namespace _1RM.Model.ProtocolRunner
{
    public class ExternalRunner : Runner
    {
        public class ObservableKvp<K, V> : NotifyPropertyChangedBase
        {
            private K _key;
            public K Key
            {
                get => _key;
                set => SetAndNotifyIfChanged(ref _key, value);
            }

            private V _value;
            public V Value
            {
                get => _value;
                set => SetAndNotifyIfChanged(ref _value, value);
            }

            public ObservableKvp(K key, V value)
            {
                Key = key;
                _value = value;
                _key = key;
                Value = value;
            }
        }

        public ExternalRunner(string runnerName, string ownerProtocolName) : base(runnerName, ownerProtocolName)
        {
            OwnerProtocolName = ownerProtocolName;
        }

        protected string _exePath = "";
        public string ExePath
        {
            get => _exePath;
            set
            {
                if (SetAndNotifyIfChanged(ref _exePath, value))
                {
                    RaisePropertyChanged(nameof(IsExeExisted));
                }
            }
        }

        public bool IsExeExisted
        {
            get
            {
                if (string.IsNullOrEmpty(_exePath))
                    return false;
                try
                {
                    return WinCmdRunner.CheckFileExistsAndFullName(_exePath).Item1;
                }
                catch
                {
                    // ignored
                }

                return false;
            }
        }

        protected string _arguments = "";
        public string Arguments
        {
            get => _arguments;
            set => SetAndNotifyIfChanged(ref _arguments, value);
        }


        protected bool _runWithHosting = false;
        public bool RunWithHosting
        {
            get => _runWithHosting;
            set => SetAndNotifyIfChanged(ref _runWithHosting, value);
        }

        private ObservableCollection<ObservableKvp<string, string>> _environmentVariables = new ObservableCollection<ObservableKvp<string, string>>();
        public ObservableCollection<ObservableKvp<string, string>> EnvironmentVariables
        {
            get => _environmentVariables ??= new ObservableCollection<ObservableKvp<string, string>>();
            set => SetAndNotifyIfChanged(ref _environmentVariables, value);
        }
        

        private ObservableCollection<ObservableKvp<string, string>>? _specialCharacters = new ObservableCollection<ObservableKvp<string, string>>();
        public ObservableCollection<ObservableKvp<string, string>> SpecialCharacters
        {
            get
            {
                _specialCharacters ??= new ObservableCollection<ObservableKvp<string, string>>();
                if(ExePath.IndexOf("winscp.exe", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // for winscp special characters
                    // https://winscp.net/eng/docs/session_url#special
                    var specialCharacters = new Dictionary<string, string>
                    {
                        {"%", "%25" },
                        {";", "%3B" },
                        {":", "%3A" },
                        {" ", "%20" },
                        {"#", "%23" },
                        {"+", "%2B" },
                        {"/", "%2F" },
                        {"@", "%40" },
                    };
                    foreach (var kv in specialCharacters)
                    {
                        if(_specialCharacters.All(x=>x.Key != kv.Key))
                            _specialCharacters.Add(new ObservableKvp<string, string>(kv.Key, kv.Value));
                    }
                }
                return _specialCharacters;
            }
            set => SetAndNotifyIfChanged(ref _specialCharacters, value);
        }

        public void ApplySpecialCharacters(ProtocolBase protocol)
        {
            if (SpecialCharacters == null || SpecialCharacters.Count == 0) return;
            if (protocol is not ProtocolBaseWithAddressPortUserPwd p) return;
            p.DecryptToConnectLevel();
            foreach (var kv in SpecialCharacters)
            {
                if (string.IsNullOrEmpty(kv.Key)) continue;
                p.UserName = p.UserName.Replace(kv.Key, kv.Value);
                p.Password = p.Password.Replace(kv.Key, kv.Value);
            }
        }

        /// <summary>
        /// Marco names for auto complete use
        /// </summary>
        [JsonIgnore]
        public List<string> MarcoNames { get; set; } = new();

        [Obsolete]
        public Dictionary<string, string> Params = new Dictionary<string, string>();
    }
}
