using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using _1RM.Model.DAO;

namespace _1RM.Service.DataSource.Model
{
    public class MysqlConfig : DataSourceConfigBase
    {
        private string _host = "127.0.0.1";
        private int _port = 3306;
        private string _dbName = "1Remote";
        private string _userName = "";
        private string _password = "";

        public string Host
        {
            get => _host;
            set => SetAndNotifyIfChanged(ref _host, value);
        }

        public int Port
        {
            get => _port;
            set => SetAndNotifyIfChanged(ref _port, value);
        }

        public string DbName
        {
            get => _dbName;
            set => SetAndNotifyIfChanged(ref _dbName, value);
        }

        public string UserName
        {
            get => _userName;
            set => SetAndNotifyIfChanged(ref _userName, value);
        }

        public string Password
        {
            get => _password;
            set => SetAndNotifyIfChanged(ref _password, value);
        }

        public MysqlConfig(string name) : base(name)
        {
        }

        public override string GetConnectionString()
        {
            return DbExtensions.GetMysqlConnectionString(_host, _port, _dbName, _userName, _password);
        }

        public override DatabaseType DatabaseType => DatabaseType.MySql;
    }
}
