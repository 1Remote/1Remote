using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using _1RM.Model.DAO;
using JsonKnownTypes;
using Newtonsoft.Json;
using Shawn.Utils;

namespace _1RM.Service.DataSource.Model
{

    [JsonConverter(typeof(JsonKnownTypesConverter<DataSourceConfigBase>))] // json serialize/deserialize derived types https://stackoverflow.com/a/60296886/8629624
    [JsonKnownType(typeof(MysqlConfig), nameof(MysqlConfig))]
    [JsonKnownType(typeof(SqliteConfig), nameof(SqliteConfig))]
    public abstract class DataSourceConfigBase : NotifyPropertyChangedBase, ICloneable
    {
        protected DataSourceConfigBase(string name)
        {
            _name = name;
            this.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName != nameof(Description))
                {
                    RaisePropertyChanged(nameof(Description));
                }
            };
        }

        public void SetStatus(bool isOk, string msg)
        {
            IsOk = isOk;
            StatusInfo = msg;
        }

        private bool _isOk = false;
        [JsonIgnore]
        public bool IsOk
        {
            get => _isOk;
            set => SetAndNotifyIfChanged(ref _isOk, value);
        }


        private string _statusInfo = "";
        [JsonIgnore]
        public string StatusInfo
        {
            get => _statusInfo;
            set => SetAndNotifyIfChanged(ref _statusInfo, value);
        }


        private string _name;
        public string Name
        {
            get => _name;
            set => SetAndNotifyIfChanged(ref _name, value);
        }

        public abstract string GetConnectionString();

        [JsonIgnore]
        public abstract DatabaseType DatabaseType { get; }

        [JsonIgnore]
        public abstract string Description { get; }
        public object Clone()
        {
            return MemberwiseClone();
        }
    }
}
