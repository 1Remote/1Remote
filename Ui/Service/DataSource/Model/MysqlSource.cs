using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using _1RM.Model.DAO;
using _1RM.Model.DAO.Dapper;
using _1RM.Utils;
using Newtonsoft.Json;

namespace _1RM.Service.DataSource.Model
{
    public sealed class MysqlSource : DataSourceBase
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

        [JsonProperty(nameof(Password))]
        private string EncryptPassword { get; set; } = "";

        [JsonIgnore]
        public string Password
        {
            get
            {
                if (string.IsNullOrEmpty(EncryptPassword))
                    return "";
                var t = UnSafeStringEncipher.SimpleDecrypt(EncryptPassword);
                if (string.IsNullOrEmpty(t))
                    return EncryptPassword;
                return t ?? "";
            }
            set
            {
                EncryptPassword = string.IsNullOrEmpty(value) ? "" : UnSafeStringEncipher.SimpleEncrypt(value);
                var t = UnSafeStringEncipher.SimpleDecrypt(EncryptPassword);
                RaisePropertyChanged();
            }
        }

        public MysqlSource() : base()
        {
        }

        public override string GetConnectionString(int connectTimeOutSeconds = 5)
        {
            return DbExtensions.GetMysqlConnectionString(_host, _port, _databaseName, _userName, Password, connectTimeOutSeconds);
        }

        [JsonIgnore]
        public override DatabaseType DatabaseType => DatabaseType.MySql;

        [JsonIgnore] public override string Description => $"server={Host};port={Port};database={DatabaseName};Uid={UserName};...";




        private IDatabase? _database = null;
        public override IDatabase GetDataBase()
        {
            _database ??= new DapperDatabase(DatabaseName, DatabaseType.MySql);
            _database.DatabaseName = DatabaseName;
            return _database;
        }

        public static bool TestConnection(MysqlSource config)
        {
            return TestConnection(config.Host, config.Port, config.DatabaseName, config.UserName, config.Password);
        }
        public static bool TestConnection(string host, int port, string dbName, string userName, string password)
        {
            var str = DbExtensions.GetMysqlConnectionString(host, port, dbName, userName, password, 2);
            var db = new DapperDatabase(dbName, DatabaseType.MySql);
            var ret = db.OpenNewConnection(str);
            db.CloseConnection();
            return ret.IsSuccess;
        }
    }
}
