using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using _1RM.Model;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.DAO;
using _1RM.Service.DataSource.Model;
using _1RM.Utils;
using _1RM.View.Utils;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.FileSystem;

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
                    if (o is not string type
                        || _configurationService.AdditionalDataSource.Count >= 2)
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
                }, _ =>
                        IoPermissionHelper.HasWritePermissionOnFile(AppPathHelper.Instance.ProfileAdditionalDataSourceJsonPath)
                        && _configurationService.AdditionalDataSource.Count < 2
                    );
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
                        if (true == MessageBoxHelper.Confirm(IoC.Get<ILanguageService>().Translate("confirm_to_delete_selected")))
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
                }, _ =>
                    IoPermissionHelper.HasWritePermissionOnFile(AppPathHelper.Instance.ProfileAdditionalDataSourceJsonPath));
            }
        }




        private RelayCommand? _cmdRefreshDataSource;
        public RelayCommand CmdRefreshDataSource
        {
            get
            {
                return _cmdRefreshDataSource ??= new RelayCommand((o) =>
                {
                    if (o is DataSourceBase dataSource)
                    {
                        MaskLayerController.ShowProcessingRing();
                        if (dataSource.Status != EnumDatabaseStatus.OK)
                        {
                            dataSource.ReconnectTime = DateTime.MinValue;
                        }
                        else
                        {
                            IoC.Get<GlobalData>().CheckUpdateTime = DateTime.MinValue;
                        }

                        Task.Factory.StartNew(() =>
                        {
                            try
                            {
                                _dataSourceService.AddOrUpdateDataSource(dataSource);
                            }
                            finally
                            {
                                MaskLayerController.HideMask();
                            }
                        });
                    }
                });
            }
        }

    }
}
