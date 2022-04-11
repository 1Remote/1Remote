using System;
using System.IO;
using System.Linq;
using JsonKnownTypes;
using Newtonsoft.Json;
using PRM.Model.ProtocolRunner.Default;
using Shawn.Utils;

namespace PRM.Model.ProtocolRunner
{
    [JsonConverter(typeof(JsonKnownTypesConverter<Runner>))] // json serialize/deserialize derived types https://stackoverflow.com/a/60296886/8629624
    [JsonKnownType(typeof(Runner), nameof(Runner))]
    [JsonKnownType(typeof(ExternalRunner), nameof(ExternalRunner))]
    [JsonKnownType(typeof(KittyRunner), nameof(KittyRunner))]
    [JsonKnownType(typeof(InternalDefaultRunner), nameof(InternalDefaultRunner))]
    public class Runner : NotifyPropertyChangedBase, ICloneable
    {
        public Runner(string runnerName)
        {
            Name = runnerName?.Trim();
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

        public object Clone()
        {
            return this.MemberwiseClone();
        }

        //[JsonIgnore]
        //public bool IsRemovable = true;
    }
}
