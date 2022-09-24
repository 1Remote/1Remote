using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using _1RM.Controls;
using _1RM.Model;
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
            _dataSourceService = IoC.Get<DataSourceService>();

            LocalSource = _configurationService.DataSource.LocalDataSource;
            _sourceConfigs.Add(LocalSource);

            foreach (var config in _configurationService.DataSource.AdditionalDataSourceConfigs)
            {
                _sourceConfigs.Add(config);
            }
        }


        private RelayCommand? _cmdApply;
        public RelayCommand CmdApply => _cmdApply ??= new RelayCommand((o) =>
        {
            // 保存配置修改
        });


        public SqliteSource LocalSource { get; }

        private ObservableCollection<DataSourceBase> _sourceConfigs = new ObservableCollection<DataSourceBase>();
        public ObservableCollection<DataSourceBase> SourceConfigs
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
                                    SourceConfigs.Add(vm.New);
                                    _configurationService.DataSource.AdditionalDataSourceConfigs.Add(vm.New);
                                    _configurationService.Save();
                                }
                                break;
                            }
                        case "mysql":
                            {
                                var vm = new MysqlSettingViewModel(this);
                                if (IoC.Get<IWindowManager>().ShowDialog(vm, IoC.Get<MainWindowViewModel>()) == true)
                                {
                                    SourceConfigs.Add(vm.New);
                                    _configurationService.DataSource.AdditionalDataSourceConfigs.Add(vm.New);
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
                        case SqliteSource sqliteConfig:
                            {
                                var vm = new SqliteSettingViewModel(this, sqliteConfig);
                                if (IoC.Get<IWindowManager>().ShowDialog(vm, IoC.Get<MainWindowViewModel>()) == true)
                                {
                                    GlobalEventHelper.ShowProcessingRing?.Invoke(Visibility.Visible, "");
                                    _configurationService.Save();
                                    Task.Factory.StartNew(() =>
                                    {
                                        _dataSourceService.AddOrUpdateDataSource(sqliteConfig);
                                        GlobalEventHelper.ShowProcessingRing?.Invoke(Visibility.Collapsed, "");
                                    });
                                }
                                break;
                            }
                        case MysqlSource mysqlConfig:
                            {
                                var vm = new MysqlSettingViewModel(this, mysqlConfig);
                                if (IoC.Get<IWindowManager>().ShowDialog(vm, IoC.Get<MainWindowViewModel>()) == true)
                                {
                                    GlobalEventHelper.ShowProcessingRing?.Invoke(Visibility.Visible, "");
                                    _configurationService.Save();
                                    Task.Factory.StartNew(() =>
                                    {
                                        _dataSourceService.AddOrUpdateDataSource(mysqlConfig);
                                        GlobalEventHelper.ShowProcessingRing?.Invoke(Visibility.Collapsed, "");
                                    });
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
                    if (o is DataSourceBase configBase && configBase != LocalSource)
                    {
                        if (true == MessageBoxHelper.Confirm(IoC.Get<ILanguageService>().Translate("confirm_to_delete_selected")))
                        {
                            if (_configurationService.DataSource.AdditionalDataSourceConfigs.Contains(configBase))
                            {
                                _configurationService.DataSource.AdditionalDataSourceConfigs.Remove(configBase);
                                _configurationService.Save();
                            }
                            SourceConfigs.Remove(configBase);
                            _dataSourceService.RemoveDataSource(configBase.DataSourceName);
                        }
                    }
                });
            }
        }
    }
}
