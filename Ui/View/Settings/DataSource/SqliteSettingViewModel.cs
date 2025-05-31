using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using _1RM.Service;
using _1RM.Service.DataSource.DAO;
using _1RM.Service.DataSource.Model;
using _1RM.Utils;
using _1RM.Utils.Tracing;
using _1RM.View.Utils.MaskAndPop;
using Newtonsoft.Json;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.FileSystem;

namespace _1RM.View.Settings.DataSource
{
    public class SqliteSettingViewModel : PopupBase, IDataErrorInfo
    {
        public SqliteSource? OrgSqliteConfig { get; } = null;

        public readonly SqliteSource New;
        private readonly DataSourceViewModel _dataSourceViewModel;

        public SqliteSettingViewModel(DataSourceViewModel dataSourceViewModel, SqliteSource? sqliteConfig = null)
        {
            OrgSqliteConfig = sqliteConfig;
            _dataSourceViewModel = dataSourceViewModel;

            // Edit mode
            if (OrgSqliteConfig != null)
            {
                New = new SqliteSource(OrgSqliteConfig.Name);
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
            else
            {
                New = new SqliteSource("tmp");
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
            if (res.Status == EnumDatabaseStatus.OK)
            {
                return true;
            }
            if (showAlert == false) return false;
            MessageBoxHelper.ErrorAlert(res.GetErrorMessage, ownerViewModel: this);
            return false;
        }


        public bool NameWritable { get; } = true;
        private string _name = "";
        public string Name
        {
            get => _name;
            set
            {
                if (NameWritable) SetAndNotifyIfChanged(ref _name, value);
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
                        MessageBoxHelper.ErrorAlert(IoC.Translate("string_database_error_permission_denied"), ownerViewModel: this);
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
                            UnifyTracing.Error(ee);
                            MessageBoxHelper.ErrorAlert(ee.Message, ownerViewModel: this);
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

                }, o => CanSave());
            }
        }

        public bool CanSave()
        {
            if (!string.IsNullOrEmpty(this[nameof(Path)])
                || !string.IsNullOrEmpty(this[nameof(Name)])
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
                }, o => OrgSqliteConfig == null);
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
                            if (_dataSourceViewModel.SourceConfigs.Any(x => x != OrgSqliteConfig && string.Equals(x.DataSourceName.Trim(), _name.Trim(), StringComparison.CurrentCultureIgnoreCase)))
                                return IoC.Translate(LanguageService.XXX_IS_ALREADY_EXISTED, _name);
                            break;
                        }
                    case nameof(Path):
                        {
                            if (string.IsNullOrWhiteSpace(Path))
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
