using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using _1RM.Service;
using _1RM.Service.DataSource.Model;
using _1RM.Utils;
using _1RM.View.Utils;
using _1RM.View.Utils.MaskAndPop;
using Newtonsoft.Json;
using Shawn.Utils.Wpf;

namespace _1RM.View.Settings.DataSource
{
    public class PgsqlSettingViewModel : PopupBase, IDataErrorInfo
    {
        private readonly PgsqlSource? _orgPgsqlConfig = null;
        public PgsqlSource New = new PgsqlSource();
        private readonly DataSourceViewModel _dataSourceViewModel;
        public PgsqlSettingViewModel(DataSourceViewModel dataSourceViewModel, PgsqlSource? pgsqlConfig = null)
        {
            _dataSourceViewModel = dataSourceViewModel;
            _orgPgsqlConfig = pgsqlConfig;
            if (_orgPgsqlConfig != null)
            {
                Name = _orgPgsqlConfig.DataSourceName;
                Host = _orgPgsqlConfig.Host;
                Port = _orgPgsqlConfig.Port.ToString();
                DatabaseName = _orgPgsqlConfig.DatabaseName;
                UserName = _orgPgsqlConfig.UserName;
                Password = _orgPgsqlConfig.Password;
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
            set => SetAndNotifyIfChanged(ref _name, value);

        }

        private string _host = "127.0.0.1";
        public string Host
        {
            get => _host;
            set => SetAndNotifyIfChanged(ref _host, value);
        }

        private string _port = "5432";
        public string Port
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
        
        private RelayCommand? _cmdSave;
        public RelayCommand CmdSave
        {
            get
            {
                return _cmdSave ??= new RelayCommand((o) =>
                {
                    if (_orgPgsqlConfig != null)
                    {
                        _orgPgsqlConfig.DataSourceName = Name.Trim();
                        _orgPgsqlConfig.Host = Host.Trim();
                        _orgPgsqlConfig.Port = int.Parse(_port);
                        _orgPgsqlConfig.DatabaseName = DatabaseName.Trim();
                        _orgPgsqlConfig.UserName = UserName.Trim();
                        _orgPgsqlConfig.Password = Password;
                    }
                    else
                    {
                        New = new PgsqlSource()
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

                }, o => CanSave());
            }
        }

        public bool CanSave()
        {
            if (!string.IsNullOrEmpty(this[nameof(Host)])
                || !string.IsNullOrEmpty(this[nameof(Port)])
                || !string.IsNullOrEmpty(this[nameof(Name)])
                || !string.IsNullOrEmpty(this[nameof(DatabaseName)])
                || !string.IsNullOrEmpty(this[nameof(UserName)])
                || !string.IsNullOrEmpty(this[nameof(Password)])
               )
                return false;
            return true;
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
                        MaskLayerController.ShowProcessingRing(assignLayerContainer: this);
                        try
                        {
                            var config = new PgsqlSource()
                            {
                                DataSourceName = Name.Trim(),
                                Host = Host.Trim(),
                                Port = int.Parse(_port),
                                DatabaseName = DatabaseName.Trim(),
                                UserName = UserName,
                                Password = Password
                            };
                            if (PgsqlSource.TestConnection(config))
                            {
                                MessageBoxHelper.Info(IoC.Translate("Success"), ownerViewModel: this);
                            }
                            else
                            {
                                MessageBoxHelper.Info(IoC.Translate("Failed"), ownerViewModel: this);
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

        #region IDataErrorInfo
        [JsonIgnore] public string Error => "";

        [JsonIgnore]
        public string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(Name):
                        {
                            if (string.IsNullOrWhiteSpace(_name))
                                return IoC.Translate(LanguageService.CAN_NOT_BE_EMPTY);
                            if (_dataSourceViewModel.SourceConfigs.Any(x => x != _orgPgsqlConfig && string.Equals(x.DataSourceName.Trim(), _name.Trim(), StringComparison.CurrentCultureIgnoreCase)))
                                return IoC.Translate(LanguageService.XXX_IS_ALREADY_EXISTED, _name);
                            break;
                        }
                    case nameof(Host):
                        {
                            if (string.IsNullOrWhiteSpace(Host))
                                return IoC.Translate(LanguageService.CAN_NOT_BE_EMPTY);
                            break;
                        }
                    case nameof(Port):
                        {
                            if (string.IsNullOrWhiteSpace(Port))
                                return IoC.Translate(LanguageService.CAN_NOT_BE_EMPTY);
                            if (int.TryParse(_port, out var p) == false || p < 0 || p > 65535)
                                return "1 - 65535";
                            break;
                        }
                    case nameof(DatabaseName):
                        {
                            if (string.IsNullOrWhiteSpace(DatabaseName))
                                return IoC.Translate(LanguageService.CAN_NOT_BE_EMPTY);
                            break;
                        }
                    case nameof(UserName):
                        {
                            if (string.IsNullOrWhiteSpace(UserName))
                                return IoC.Translate(LanguageService.CAN_NOT_BE_EMPTY);
                            break;
                        }
                    case nameof(Password):
                        {
                            if (string.IsNullOrWhiteSpace(Password))
                                return IoC.Translate(LanguageService.CAN_NOT_BE_EMPTY);
                            break;
                        }
                }
                return "";
            }
        }
        #endregion
    }
}
