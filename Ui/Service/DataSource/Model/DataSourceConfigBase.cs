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

        private string _name;
        public string Name
        {
            get => _name;
            set => SetAndNotifyIfChanged(ref _name, value);
        }

        public abstract string GetConnectionString();
        public abstract DatabaseType DatabaseType { get; }

        public abstract string Description { get; }
        public object Clone()
        {
            return MemberwiseClone();
        }
    }
}
