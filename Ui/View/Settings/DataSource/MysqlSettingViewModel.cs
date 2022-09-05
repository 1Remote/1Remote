using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using _1RM.Model;
using _1RM.Model.DAO;
using _1RM.Model.DAO.Dapper;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.Utils;
using com.github.xiangyuecn.rsacsharp;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.FileSystem;

namespace _1RM.View.Settings.DataSource
{
    public class MysqlSettingViewModel : NotifyPropertyChangedBase
    {
        public MysqlConfig MysqlConfig { get; }
        private MysqlDatabaseSource _databaseSource;
        private readonly ILanguageService _languageService = IoC.Get<ILanguageService>();
        public MysqlSettingViewModel(MysqlConfig mysqlConfig)
        {
            MysqlConfig = mysqlConfig;
            _databaseSource = new MysqlDatabaseSource("tmp", mysqlConfig);
            ValidateDbStatusAndShowMessageBox(false);
        }

        private bool ValidateDbStatusAndShowMessageBox(bool showAlert = true)
        {
            // validate rsa key
            var res = _databaseSource.Database_SelfCheck();
            if (res == EnumDbStatus.OK)
            {
                return true;
            }
            if (showAlert == false) return true;
            MessageBoxHelper.ErrorAlert(res.GetErrorInfo());
            return false;
        }
    }
}
