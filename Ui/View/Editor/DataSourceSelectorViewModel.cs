using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using _1RM.Model;
using _1RM.Service.DataSource.DAO;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.Utils;
using _1RM.View.Utils;
using Shawn.Utils.Wpf;
using Stylet;

namespace _1RM.View.Editor
{
    public class DataSourceSelectorViewModel : NotifyPropertyChangedBaseScreen
    {
        private readonly DataSourceService _dataSourceService;
        protected DataSourceSelectorViewModel()
        {
            var configurationService = IoC.Get<ConfigurationService>();
            _dataSourceService = IoC.Get<DataSourceService>();

            Debug.Assert(configurationService.AdditionalDataSource.Count > 0);

            _sourceConfigs.Add(configurationService.LocalDataSource);
            _sourceConfigs.AddRange(configurationService.AdditionalDataSource);
            _selectedSource = _sourceConfigs.First();
        }


        private List<DataSourceBase> _sourceConfigs = new List<DataSourceBase>();
        public List<DataSourceBase> SourceConfigs
        {
            get => _sourceConfigs;
            set => SetAndNotifyIfChanged(ref _sourceConfigs, value);
        }




        private DataSourceBase _selectedSource;
        public DataSourceBase SelectedSource
        {
            get => _selectedSource;
            set => SetAndNotifyIfChanged(ref _selectedSource, value);
        }


        private RelayCommand? _cmdSelect;
        public RelayCommand CmdSelect
        {
            get
            {
                return _cmdSelect ??= new RelayCommand((o) =>
                {
                    if (o is DataSourceBase selected)
                    {
                        SelectedSource = selected;
                        this.RequestClose(true);
                    }
                });
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


        private RelayCommand? _cmdRefreshDataSource;
        public RelayCommand CmdRefreshDataSource
        {
            get
            {
                return _cmdRefreshDataSource ??= new RelayCommand((o) =>
                {
                    if (o is DataSourceBase dataSource)
                    {
                        if (dataSource.Status != EnumDatabaseStatus.OK)
                        {
                            dataSource.ReconnectTime = DateTime.MinValue;
                        }
                        else
                        {
                            IoC.Get<GlobalData>().CheckUpdateTime = DateTime.MinValue;
                        }
                    }
                });
            }
        }


        public static DataSourceBase? SelectDataSource()
        {
            if (IoC.Get<ConfigurationService>().AdditionalDataSource.Any())
            {
                var vm = new DataSourceSelectorViewModel();
                if (MaskLayerController.ShowDialogWithMask(vm) != true)
                    return null;
                return IoC.Get<DataSourceService>().GetDataSource(vm.SelectedSource.DataSourceName);
            }
            else
            {
                return IoC.Get<DataSourceService>().LocalDataSource;
            }
        }
    }
}
