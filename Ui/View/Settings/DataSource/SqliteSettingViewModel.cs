using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using _1RM.Model.DAO;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.Utils;
using Shawn.Utils;

namespace _1RM.View.Settings.DataSource
{
    public class SqliteSettingViewModel : NotifyPropertyChangedBase
    {
        public SqliteConfig SqliteConfig { get; }
        private SqliteDatabaseSource _databaseSource;
        public SqliteSettingViewModel(SqliteConfig sqliteConfig)
        {
            SqliteConfig = sqliteConfig;
            _databaseSource = new SqliteDatabaseSource("tmp", sqliteConfig);
            ValidateDbStatusAndShowMessageBox(false);
        }

        private bool ValidateDbStatusAndShowMessageBox(bool showAlert = true)
        {
            // validate rsa key
            var res = _databaseSource.Database_SelfCheck();
            if (res == EnumDbStatus.OK)
            {
                DbRsaPublicKey = _databaseSource.Database_GetPublicKey() ?? "";
                DbRsaPrivateKeyPath = _databaseSource.Database_GetPrivateKeyPath() ?? "";
                return true;
            }
            if (showAlert == false) return true;
            MessageBoxHelper.ErrorAlert(res.GetErrorInfo());
            return false;
        }


        private string _dbRsaPublicKey = "";
        public string DbRsaPublicKey
        {
            get => _dbRsaPublicKey;
            private set => SetAndNotifyIfChanged(ref _dbRsaPublicKey, value);
        }
        private string _dbRsaPrivateKeyPath = "";
        public string DbRsaPrivateKeyPath
        {
            get => _dbRsaPrivateKeyPath;
            set => SetAndNotifyIfChanged(ref _dbRsaPrivateKeyPath, value);
        }
    }
}
