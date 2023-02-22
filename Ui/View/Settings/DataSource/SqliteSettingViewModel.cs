using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using _1RM.View.Utils;
using com.github.xiangyuecn.rsacsharp;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.FileSystem;
using Stylet;

namespace _1RM.View.Settings.DataSource
{
    public class SqliteSettingViewModel : MaskLayerContainerScreenBase
    {
        public SqliteSource? OrgSqliteConfig { get; } = null;

        public readonly SqliteSource New = new SqliteSource();
        private readonly DataSourceViewModel _dataSourceViewModel;

        public SqliteSettingViewModel(DataSourceViewModel dataSourceViewModel, SqliteSource? sqliteConfig = null)
        {
            OrgSqliteConfig = sqliteConfig;
            _dataSourceViewModel = dataSourceViewModel;

            // Edit mode
            if (OrgSqliteConfig != null)
            {
                Name = OrgSqliteConfig.DataSourceName;
                Path = OrgSqliteConfig.Path;
                New.Database_SelfCheck();
                // disable name editing of LocalSource
                if (dataSourceViewModel.LocalSource == sqliteConfig)
                {
                    NameWritable = false;
                }
                OrgSqliteConfig?.Database_CloseConnection();
            }
        }

        protected override void OnClose()
        {
            base.OnClose();
            New.Database_CloseConnection();
        }

        private bool ValidateDbStatusAndShowMessageBox(bool showAlert = true)
        {
            var res = New.Database_SelfCheck();
            if (res == EnumDatabaseStatus.OK)
            {
                return true;
            }
            if (showAlert == false) return false;
            MessageBoxHelper.ErrorAlert(res.GetErrorInfo(), ownerViewModel: this);
            return false;
        }


        public bool NameWritable { get; } = true;
        private string _name = "";
        public string Name
        {
            get => _name;
            set
            {
                if (NameWritable && SetAndNotifyIfChanged(ref _name, value))
                {
                    New.DataSourceName = value;
                    if (string.IsNullOrWhiteSpace(_name))
                        throw new ArgumentException(IoC.Get<ILanguageService>().Translate("Can not be empty!"));
                    if (_dataSourceViewModel.SourceConfigs.Any(x => x != OrgSqliteConfig && string.Equals(x.DataSourceName, _name, StringComparison.CurrentCultureIgnoreCase)))
                        throw new ArgumentException(IoC.Get<ILanguageService>().Translate("{0} is existed!", _name));
                }
            }
        }

        private string _path = "";
        public string Path
        {
            get => _path;
            set
            {
                if (SetAndNotifyIfChanged(ref _path, value))
                {
                    New.Database_CloseConnection();
                    New.Path = value;
                }
            }
        }


        private RelayCommand? _cmdSelectDbPath;
        public RelayCommand CmdSelectDbPath
        {
            get
            {
                return _cmdSelectDbPath ??= new RelayCommand((o) =>
                {
                    var newPath = SelectFileHelper.OpenFile(
                        initialDirectory: string.IsNullOrWhiteSpace(Path) == false && File.Exists(Path) ? new FileInfo(Path).DirectoryName : "",
                        filter: "SqliteSource Database|*.db",
                        checkFileExists: false);
                    if (newPath == null) return;
                    var oldPath = Path;
                    if (string.Equals(newPath, oldPath, StringComparison.CurrentCultureIgnoreCase))
                        return;

                    if (!IoPermissionHelper.HasWritePermissionOnFile(newPath))
                    {
                        MessageBoxHelper.ErrorAlert(IoC.Get<ILanguageService>().Translate("string_database_error_permission_denied"), ownerViewModel: this);
                        return;
                    }
                    
                    Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            Path = newPath;
                            ValidateDbStatusAndShowMessageBox();
                        }
                        catch (Exception ee)
                        {
                            Path = oldPath;
                            SimpleLogHelper.Warning(ee);
                            MessageBoxHelper.ErrorAlert(IoC.Get<ILanguageService>().Translate("system_options_data_security_error_can_not_open"), ownerViewModel: this);
                        }
                    });
                });
            }
        }


        private RelayCommand? _cmdSave;
        public RelayCommand CmdSave
        {
            get
            {
                return _cmdSave ??= new RelayCommand((o) =>
                {
                    if (ValidateDbStatusAndShowMessageBox() == false)
                        return;
                    var ret = false;
                    if (OrgSqliteConfig != null)
                    {
                        if (Name != OrgSqliteConfig.DataSourceName
                            || OrgSqliteConfig.Path != Path)
                        {
                            ret = true;
                        }
                        OrgSqliteConfig.DataSourceName = Name;
                        OrgSqliteConfig.Path = Path;
                    }
                    else
                    {
                        ret = true;
                    }

                    this.RequestClose(ret);

                }, o => (
                    string.IsNullOrWhiteSpace(Name) == false
                    && string.IsNullOrWhiteSpace(Path) == false
                    && File.Exists(Path)));
            }
        }



        private RelayCommand? _cmdCancel;
        public RelayCommand CmdCancel
        {
            get
            {
                return _cmdCancel ??= new RelayCommand((o) =>
                {
                    New.Database_CloseConnection();
                    this.RequestClose(false);
                }, o => OrgSqliteConfig == null);
            }
        }
    }
}
