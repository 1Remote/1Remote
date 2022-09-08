using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using _1RM.Controls;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.Utils;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Stylet;

namespace _1RM.View.Settings.DataSource
{
    public class DataSourceViewModel : NotifyPropertyChangedBase
    {
        private readonly ConfigurationService _configurationService;
        private readonly DataSourceService _dataSourceService;

        public DataSourceViewModel()
        {
            _configurationService = IoC.Get<ConfigurationService>();
            LocalSourceConfig = _configurationService.DataSource.LocalDataSourceConfig;
            _sourceConfigs.Add(LocalSourceConfig);

            foreach (var config in _configurationService.DataSource.AdditionalDataSourceConfigs)
            {
                _sourceConfigs.Add(config);
            }

            _dataSourceService = IoC.Get<DataSourceService>();
        }


        private RelayCommand? _cmdApply;
        public RelayCommand CmdApply => _cmdApply ??= new RelayCommand((o) =>
        {
            // 保存配置修改
        });


        public SqliteConfig LocalSourceConfig { get; }

        private ObservableCollection<DataSourceConfigBase> _sourceConfigs = new ObservableCollection<DataSourceConfigBase>();
        public ObservableCollection<DataSourceConfigBase> SourceConfigs
        {
            get => _sourceConfigs;
            set => SetAndNotifyIfChanged(ref _sourceConfigs, value);
        }



        private RelayCommand? _cmdAdd;
        public RelayCommand CmdAdd
        {
            get
            {
                return _cmdAdd ??= new RelayCommand((o) =>
                {
                    if (o is not string type)
                    {
                        return;
                    }

                    switch (type.ToLower())
                    {
                        case "sqlite":
                            {
                                var vm = new SqliteSettingViewModel(this);
                                if (IoC.Get<IWindowManager>().ShowDialog(vm, IoC.Get<MainWindowViewModel>()) == true)
                                {
                                    SourceConfigs.Add(vm.NewConfig);
                                    _configurationService.DataSource.AdditionalDataSourceConfigs.Add(vm.NewConfig);
                                    _configurationService.Save();
                                }
                                break;
                            }
                        case "mysql":
                            {
                                var vm = new MysqlSettingViewModel(this);
                                if (IoC.Get<IWindowManager>().ShowDialog(vm, IoC.Get<MainWindowViewModel>()) == true)
                                {
                                    SourceConfigs.Add(vm.NewConfig);
                                    _configurationService.DataSource.AdditionalDataSourceConfigs.Add(vm.NewConfig);
                                    _configurationService.Save();
                                }
                                break;
                            }
                        default:
                            throw new ArgumentOutOfRangeException($"{type} is not a vaild type");
                    }
                });
            }
        }




        private RelayCommand? _cmdEdit;
        public RelayCommand CmdEdit
        {
            get
            {
                return _cmdEdit ??= new RelayCommand((o) =>
                {
                    switch (o)
                    {
                        case SqliteConfig sqliteConfig:
                            {
                                var vm = new SqliteSettingViewModel(this, sqliteConfig);
                                if (IoC.Get<IWindowManager>().ShowDialog(vm, IoC.Get<MainWindowViewModel>()) == true)
                                {
                                    _configurationService.Save();
                                    _dataSourceService.AddOrUpdateDataSource(sqliteConfig);
                                }
                                break;
                            }
                        case MysqlConfig mysqlConfig:
                            {
                                var vm = new MysqlSettingViewModel(this, mysqlConfig);
                                if (IoC.Get<IWindowManager>().ShowDialog(vm, IoC.Get<MainWindowViewModel>()) == true)
                                {
                                    _configurationService.Save();
                                    _dataSourceService.AddOrUpdateDataSource(mysqlConfig);
                                }
                                break;
                            }
                        default:
                            throw new NotSupportedException($"{o?.GetType()} is not a supported type");
                    }
                });
            }
        }






        private RelayCommand? _cmdDelete;
        public RelayCommand CmdDelete
        {
            get
            {
                return _cmdDelete ??= new RelayCommand((o) =>
                {
                    if (o is DataSourceConfigBase configBase && configBase != LocalSourceConfig)
                    {
                        if (true == MessageBoxHelper.Confirm(IoC.Get<ILanguageService>().Translate("confirm_to_delete_selected")))
                        {
                            if (_configurationService.DataSource.AdditionalDataSourceConfigs.Contains(configBase))
                            {
                                _configurationService.DataSource.AdditionalDataSourceConfigs.Remove(configBase);
                                _configurationService.Save();
                            }
                            SourceConfigs.Remove(configBase);
                            _dataSourceService.RemoveDataSource(configBase.Name);
                        }
                    }
                });
            }
        }
    }
}
