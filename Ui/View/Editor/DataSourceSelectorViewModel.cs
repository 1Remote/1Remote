using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.Utils;
using Shawn.Utils.Wpf;

namespace _1RM.View.Editor
{
    public class DataSourceSelectorViewModel : NotifyPropertyChangedBaseScreen
    {
        private readonly ConfigurationService _configurationService;
        private readonly DataSourceService _dataSourceService;
        public DataSourceSelectorViewModel()
        {
            _configurationService = IoC.Get<ConfigurationService>();
            _dataSourceService = IoC.Get<DataSourceService>();

            Debug.Assert(_configurationService.DataSource.AdditionalDataSourceConfigs.Count > 0);

            _sourceConfigs.Add(_configurationService.DataSource.LocalDataSourceConfig);
            _sourceConfigs.AddRange(_configurationService.DataSource.AdditionalDataSourceConfigs);
            _selectedSourceConfig = _sourceConfigs.First();
        }


        private List<DataSourceConfigBase> _sourceConfigs = new List<DataSourceConfigBase>();
        public List<DataSourceConfigBase> SourceConfigs
        {
            get => _sourceConfigs;
            set => SetAndNotifyIfChanged(ref _sourceConfigs, value);
        }




        private DataSourceConfigBase _selectedSourceConfig;
        public DataSourceConfigBase SelectedSourceConfig
        {
            get => _selectedSourceConfig;
            set => SetAndNotifyIfChanged(ref _selectedSourceConfig, value);
        }


        private RelayCommand? _cmdSelect;
        public RelayCommand CmdSelect
        {
            get
            {
                return _cmdSelect ??= new RelayCommand((o) =>
                {
                    if (o is DataSourceConfigBase selected)
                    {
                        SelectedSourceConfig = selected;
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
    }
}
