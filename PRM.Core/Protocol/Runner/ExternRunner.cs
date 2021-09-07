using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PRM.Core.Service;

namespace PRM.Core.Protocol.Runner
{
    [JsonConverter(typeof(JsonKnownTypeConverter<BaseClass>))]
    [JsonKnownType(typeof(Base), "base")]
    [JsonKnownType(typeof(Derived), "derived")]
    public class ExternRunner : NotifyPropertyChangedBase, ICloneable
    {
        public ExternRunner(string protocol)
        {
            Protocol = protocol;
        }

        //protected override bool SetAndNotifyIfChanged<T>(ref T oldValue, T newValue, [CallerMemberName] string propertyName = null)
        //{
        //    if (SetAndNotifyIfChanged(propertyName, ref oldValue, newValue))
        //    {
        //        Save();
        //        return true;
        //    }
        //    return false;
        //}

        public string Protocol { get; }

        //public string SavePath;

        protected string _name = "";
        public string Name
        {
            get => _name;
            set
            {
                var str = value;
                string invalid = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
                str = invalid.Aggregate(str, (current, c) => current.Replace(c.ToString(), ""));
                SetAndNotifyIfChanged(ref _name, str);
            }
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

        public object Clone()
        {
            return this.MemberwiseClone();
        }

        //public void Save()
        //{
        //    Debug.Assert(string.IsNullOrEmpty(SavePath) == false);
        //    var fi = new FileInfo(SavePath);
        //    if (fi.Directory.Exists == false)
        //        fi.Directory.Create();

        //    File.WriteAllText(SavePath, JsonConvert.SerializeObject(this), Encoding.UTF8);
        //}

        //public void Load(string baseFolderPath)
        //{
        //    var fullName = Path.Combine(baseFolderPath, "SSH", this.Name + ".json");
        //    if (File.Exists(fullName))
        //    {
        //        var ret = JsonConvert.DeserializeObject<ExternRunner>(File.ReadAllText(fullName));
        //        if (ret != null && ret.Protocol == this.Protocol && this.Name == ret.Name)
        //        {
        //            this.SavePath = fullName;
        //            this.ExePath = ret.ExePath;
        //            this.Arguments = ret.Arguments;
        //        }
        //    }
        //}
    }
}
