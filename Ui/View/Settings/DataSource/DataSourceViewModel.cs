﻿using System;
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
using _1RM.View.Editor;
using _1RM.View.Utils;
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

            LocalSource = _configurationService.LocalDataSource;
            _sourceConfigs.Add(_configurationService.LocalDataSource);

            foreach (var config in _configurationService.AdditionalDataSource)
            {
                _sourceConfigs.Add(config);
            }
        }


        private RelayCommand? _cmdApply;
        public RelayCommand CmdApply => _cmdApply ??= new RelayCommand((o) =>
        {
            // 保存配置修改
        });

        public int DatabaseCheckPeriod
        {
            get => _configurationService.DatabaseCheckPeriod;
            set
            {
                if (value != _configurationService.DatabaseCheckPeriod)
                {
                    _configurationService.DatabaseCheckPeriod = value;
                    RaisePropertyChanged();
                }
            }
        }


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

                    DataSourceBase? dataSource = null;
                    switch (type.ToLower())
                    {
                        case "sqlite":
                            {
                                var vm = new SqliteSettingViewModel(this);
                                if (MaskLayerController.ShowDialogWithMask(vm, doNotHideMaskIfReturnTrue: true) != true)
                                    return;
                                dataSource = vm.New;
                                break;
                            }
                        case "mysql":
                            {
                                var vm = new MysqlSettingViewModel(this);
                                if (MaskLayerController.ShowDialogWithMask(vm, doNotHideMaskIfReturnTrue: true) != true)
                                    return;
                                dataSource = vm.New;
                                break;
                            }
                        default:
                            throw new ArgumentOutOfRangeException($"{type} is not a vaild type");
                    }
                    
                    if (dataSource != null)
                    {
                        SourceConfigs.Add(dataSource);
                        _configurationService.AdditionalDataSource.Add(dataSource);
                        _configurationService.Save();
                        Task.Factory.StartNew(() =>
                        {
                            try
                            {
                                _dataSourceService.AddOrUpdateDataSource(dataSource);
                            }
                            finally
                            {
                                MaskLayerController.HideMask(IoC.Get<MainWindowViewModel>());
                            }
                        });
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
                    if (o is not DataSourceBase dataSource) return;

                    object? vm = dataSource switch
                    {
                        SqliteSource sqliteConfig => new SqliteSettingViewModel(this, sqliteConfig),
                        MysqlSource mysqlConfig => new MysqlSettingViewModel(this, mysqlConfig),
                        _ => throw new NotSupportedException($"{o?.GetType()} is not a supported type")
                    };

                    if (MaskLayerController.ShowDialogWithMask(vm, doNotHideMaskIfReturnTrue: true) != true)
                        return;

                    _configurationService.Save();
                    Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            _dataSourceService.AddOrUpdateDataSource(dataSource);
                        }
                        finally
                        {
                            MaskLayerController.HideMask(IoC.Get<MainWindowViewModel>());
                        }
                    });
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
                        if (true == MessageBoxHelper.Confirm(IoC.Get<ILanguageService>().Translate("confirm_to_delete_selected"), ownerViewModel: this))
                        {
                            if (_configurationService.AdditionalDataSource.Contains(configBase))
                            {
                                _configurationService.AdditionalDataSource.Remove(configBase);
                                _configurationService.Save();
                            }
                            SourceConfigs.Remove(configBase);
                            Task.Factory.StartNew(() =>
                            {
                                _dataSourceService.RemoveDataSource(configBase.DataSourceName);
                            });
                        }
                    }
                });
            }
        }
    }
}
