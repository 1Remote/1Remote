using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using _1RM.Model.DAO;
using Shawn.Utils;

namespace _1RM.Service.DataSource.Model
{
    public abstract class DataSourceModel : NotifyPropertyChangedBase
    {

        protected DataSourceModel(string name)
        {
            _name = name;
        }

        private string _name;
        public string Name
        {
            get => _name;
            set => SetAndNotifyIfChanged(ref _name, value);
        }

        public abstract string GetConnectionString();
        public abstract DatabaseType DatabaseType { get; }
    }
}
