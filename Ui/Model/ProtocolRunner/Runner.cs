using System;
using System.IO;
using System.Linq;
using JsonKnownTypes;
using Newtonsoft.Json;
using _1RM.Model.ProtocolRunner.Default;
using _1RM.Service;
using Shawn.Utils;

namespace _1RM.Model.ProtocolRunner
{
    [JsonConverter(typeof(JsonKnownTypesConverter<Runner>))] // json serialize/deserialize derived types https://stackoverflow.com/a/60296886/8629624
    [JsonKnownType(typeof(Runner), nameof(Runner))]
    [JsonKnownType(typeof(ExternalRunner), nameof(ExternalRunner))]
    [JsonKnownType(typeof(KittyRunner), nameof(KittyRunner))]
    [JsonKnownType(typeof(InternalDefaultRunner), nameof(InternalDefaultRunner))]
    public class Runner : NotifyPropertyChangedBase, ICloneable
    {
        public Runner(string runnerName, string ownerProtocolName)
        {
            OwnerProtocolName = ownerProtocolName;
            _name = runnerName?.Trim() ?? "";
        }

        protected string _name = "";
        public string Name
        {
            get => _name;
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    _name = "";
                    return;
                }
                var str = value;
                string invalid = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
                str = invalid.Aggregate(str, (current, c) => current.Replace(c.ToString(), ""));
                SetAndNotifyIfChanged(ref _name, str);
            }
        }
        public string OwnerProtocolName { get; set; }

        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }
}
