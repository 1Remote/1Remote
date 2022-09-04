using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using _1RM.Service;
using _1RM.Service.DataSource.Model;
using Shawn.Utils;
using Shawn.Utils.Wpf;

namespace _1RM.View.Settings.DataSource
{
    public class DataSourceViewModel : NotifyPropertyChangedBase
    {
        private readonly ConfigurationService _configurationService;


        public DataSourceViewModel()
        {
            _configurationService = IoC.Get<ConfigurationService>();
            LocalSource = new SqliteConfig("Local");
            LocalSource.Path = _configurationService.DataSource.LocalDatabasePath;
            _sourceConfigs.Add(LocalSource);
            _sourceConfigs.AddRange(_configurationService.DataSource.AdditionalDataSourceConfigs);
            _selectedSource = LocalSource;
            _editor = new SqliteSettingViewModel(LocalSource);
        }


        private RelayCommand? _cmdApply;
        public RelayCommand CmdApply => _cmdApply ??= new RelayCommand((o) =>
        {
            // 保存配置修改
        });


        public SqliteConfig LocalSource { get; }

        private DataSourceConfigBase _selectedSource;
        public DataSourceConfigBase SelectedSource
        {
            get => _selectedSource;
            set
            {
                if (value != _selectedSource)
                {
                    if (_isChanged == true)
                    {
                        // todo confirm to save.
                    }
                    _selectedSource.PropertyChanged -= SelectedSourceOnPropertyChanged;
                    SetAndNotifyIfChanged(ref _selectedSource, value);
                    _isChanged = false;
                    _selectedSource.PropertyChanged += SelectedSourceOnPropertyChanged;

                    Editor = _selectedSource switch
                    {
                        SqliteConfig sc => new SqliteSettingViewModel(sc),
                        MysqlConfig mc => throw new NotImplementedException(),
                        _ => throw new NotImplementedException()
                    };
                }
            }
        }

        private bool _isChanged = false;
        private void SelectedSourceOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            _isChanged = true;  
        }

        private List<DataSourceConfigBase> _sourceConfigs = new List<DataSourceConfigBase>();

        public List<DataSourceConfigBase> SourceConfigs
        {
            get => _sourceConfigs;
            set => SetAndNotifyIfChanged(ref _sourceConfigs, value);
        }

        private INotifyPropertyChangedBase _editor;
        public INotifyPropertyChangedBase Editor
        {
            get => _editor;
            set => SetAndNotifyIfChanged(ref _editor, value);
        }
    }
}
