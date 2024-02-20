using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        /// <summary>
        /// Marco names for auto complete use
        /// </summary>
        [JsonIgnore]
        public List<string> MarcoNames { get; set; } = new();

        [Obsolete]
        public Dictionary<string, string> Params = new Dictionary<string, string>();
    }
}
