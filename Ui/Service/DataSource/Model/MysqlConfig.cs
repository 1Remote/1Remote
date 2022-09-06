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
        public string Host
        {
            get => _host;
            set => SetAndNotifyIfChanged(ref _host, value);
        }

        private int _port = 3306;
        public int Port
        {
            get => _port;
            set => SetAndNotifyIfChanged(ref _port, value);
        }

        private string _databaseName = "1Remote";
        public string DatabaseName
        {
            get => _databaseName;
            set => SetAndNotifyIfChanged(ref _databaseName, value);
        }

        private string _userName = "";
        public string UserName
        {
            get => _userName;
            set => SetAndNotifyIfChanged(ref _userName, value);
        }

        private string _password = "";
        public string Password
        {
            get => _password;
            set => SetAndNotifyIfChanged(ref _password, value);
        }

        public MysqlConfig(string name = "") : base(name)
        {
        }

        public override string GetConnectionString()
        {
            return DbExtensions.GetMysqlConnectionString(_host, _port, _databaseName, _userName, _password);
        }

        public override DatabaseType DatabaseType => DatabaseType.MySql;
        public override string Description => $"{UserName}@{Host}:{Port}";
    }
}
