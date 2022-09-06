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
using Org.BouncyCastle.Security;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.FileSystem;

namespace _1RM.View.Settings.DataSource
{
    public class MysqlSettingViewModel : NotifyPropertyChangedBaseScreen
    {
        private readonly MysqlConfig? _orgMysqlConfig = null;
        public MysqlConfig NewConfig = new MysqlConfig("");
        private readonly DataSourceViewModel _dataSourceViewModel;
        public MysqlSettingViewModel(DataSourceViewModel dataSourceViewModel, MysqlConfig? mysqlConfig = null)
        {
            _orgMysqlConfig = mysqlConfig;
            _dataSourceViewModel = dataSourceViewModel;
        }

        //private bool ValidateDbStatusAndShowMessageBox(bool showAlert = true)
        //{
        //    // validate rsa key
        //    var res = _databaseSource.Database_SelfCheck();
        //    if (res == EnumDbStatus.OK)
        //    {
        //        return true;
        //    }
        //    if (showAlert == false) return true;
        //    MessageBoxHelper.ErrorAlert(res.GetErrorInfo());
        //    return false;
        //}

        //protected override void OnViewLoaded()
        //{
        //    if (OrgMysqlConfig == null)
        //    {
        //        Name = string.Empty;
        //    }
        //}


        private string _name = "";
        public string Name
        {
            get => _name;
            set
            {
                if (SetAndNotifyIfChanged(ref _name, value))
                {
                    if (string.IsNullOrWhiteSpace(_name))
                        throw new ArgumentException(IoC.Get<ILanguageService>().Translate("Can not be empty!"));
                    if (_dataSourceViewModel.SourceConfigs.Any(x => x != _orgMysqlConfig && string.Equals(x.Name, _name, StringComparison.CurrentCultureIgnoreCase)))
                        throw new ArgumentException(IoC.Get<ILanguageService>().Translate("{0} is existed!", _name));
                }
            }
        }

        private string _host = "127.0.0.1";
        public string Host
        {
            get => _host;
            set => SetAndNotifyIfChanged(ref _host, value);
        }

        private string _port = "3306";
        public string Port
        {
            get => _port;
            set
            {
                if (SetAndNotifyIfChanged(ref _port, value))
                {
                    if (string.IsNullOrWhiteSpace(_port))
                        throw new ArgumentException(IoC.Get<ILanguageService>().Translate("Can not be empty!"));
                    if (int.TryParse(_port, out var p) == false || p < 0 || p > 65535)
                        throw new ArgumentException("1 - 65535!");
                }
            }
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


        private RelayCommand? _cmdSave;
        public RelayCommand CmdSave
        {
            get
            {
                return _cmdSave ??= new RelayCommand((o) =>
                {
                    if (_orgMysqlConfig != null)
                    {
                        _orgMysqlConfig.Name = Name;
                        _orgMysqlConfig.Host = Host;
                        _orgMysqlConfig.Port = int.Parse(_port);
                        _orgMysqlConfig.DatabaseName = DatabaseName;
                        _orgMysqlConfig.UserName = UserName;
                        _orgMysqlConfig.Password = Password;
                    }
                    else
                    {
                        NewConfig = new MysqlConfig(Name)
                        {
                            Host = Host,
                            Port = int.Parse(_port),
                            DatabaseName = DatabaseName,
                            UserName = UserName,
                            Password = Password
                        };
                    }

                    this.RequestClose(true);

                }, o => (
                        (int.TryParse(_port, out var p) == false || p < 0 || p > 65535) == false
                         && string.IsNullOrWhiteSpace(Name) == false
                         && string.IsNullOrWhiteSpace(Host) == false
                         && string.IsNullOrWhiteSpace(DatabaseName) == false
                         && string.IsNullOrWhiteSpace(UserName) == false
                         && string.IsNullOrWhiteSpace(Password) == false));
            }
        }



        private RelayCommand? _cmdCancel;
        public RelayCommand CmdCancel
        {
            get
            {
                return _cmdCancel ??= new RelayCommand((o) =>
                {
                    this.RequestClose(false);
                });
            }
        }
    }
}
