using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using _1RM.Model;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.Utils;
using _1RM.View.Utils;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Stylet;

namespace _1RM.View.Settings.DataSource
{
    public class MysqlSettingViewModel : MaskLayerContainerScreenBase
    {
        private readonly MysqlSource? _orgMysqlConfig = null;
        public MysqlSource New = new MysqlSource();
        private readonly DataSourceViewModel _dataSourceViewModel;
        public MysqlSettingViewModel(DataSourceViewModel dataSourceViewModel, MysqlSource? mysqlConfig = null)
        {
            _dataSourceViewModel = dataSourceViewModel;
            _orgMysqlConfig = mysqlConfig;
            if (_orgMysqlConfig != null)
            {
                Name = _orgMysqlConfig.DataSourceName;
                Host = _orgMysqlConfig.Host;
                Port = _orgMysqlConfig.Port.ToString();
                DatabaseName = _orgMysqlConfig.DatabaseName;
                UserName = _orgMysqlConfig.UserName;
                Password = _orgMysqlConfig.Password;
            }
        }

        protected override void OnClose()
        {
            base.OnClose();
            New.Database_CloseConnection();
        }


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
                    if (_dataSourceViewModel.SourceConfigs.Any(x => x != _orgMysqlConfig && string.Equals(x.DataSourceName, _name, StringComparison.CurrentCultureIgnoreCase)))
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
                        _orgMysqlConfig.DataSourceName = Name.Trim();
                        _orgMysqlConfig.Host = Host.Trim();
                        _orgMysqlConfig.Port = int.Parse(_port);
                        _orgMysqlConfig.DatabaseName = DatabaseName.Trim();
                        _orgMysqlConfig.UserName = UserName.Trim();
                        _orgMysqlConfig.Password = Password;
                    }
                    else
                    {
                        New = new MysqlSource()
                        {
                            DataSourceName = Name.Trim(),
                            Host = Host.Trim(),
                            Port = int.Parse(_port),
                            DatabaseName = DatabaseName.Trim(),
                            UserName = UserName.Trim(),
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
                         && string.IsNullOrWhiteSpace(Password) == false
                         && (Name != _orgMysqlConfig?.DataSourceName
                            || Host != _orgMysqlConfig?.Host
                            || Port != _orgMysqlConfig?.Port.ToString()
                            || DatabaseName != _orgMysqlConfig?.DatabaseName
                            || UserName != _orgMysqlConfig?.UserName
                            || Password != _orgMysqlConfig?.Password
                            )
                        ));
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



        private RelayCommand? _cmdTestConnection;
        public RelayCommand CmdTestConnection
        {
            get
            {
                return _cmdTestConnection ??= new RelayCommand((o) =>
                {
                    Task.Factory.StartNew(() =>
                    {
                        var id = MaskLayerController.ShowProcessingRing(assignLayerContainer: this);
                        try
                        {
                            var config = new MysqlSource()
                            {
                                DataSourceName = Name,
                                Host = Host,
                                Port = int.Parse(_port),
                                DatabaseName = DatabaseName,
                                UserName = UserName,
                                Password = Password
                            };
                            if (MysqlSource.TestConnection(config))
                            {
                                MessageBoxHelper.Info(IoC.Get<ILanguageService>().Translate("Success!"), ownerViewModel: this);
                            }
                            else
                            {
                                MessageBoxHelper.Info(IoC.Get<ILanguageService>().Translate("Failed!"), ownerViewModel: this);
                            }
                        }
                        catch (Exception e)
                        {
                            MessageBoxHelper.ErrorAlert(e.Message, ownerViewModel: this);
                        }
                        finally
                        {
                            MaskLayerController.HideMask(this);
                        }
                    });
                });
            }
        }
    }
}
