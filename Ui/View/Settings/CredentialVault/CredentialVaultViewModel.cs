using System.Collections.ObjectModel;
using System.Linq;
using _1RM.Model;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.Utils;
using _1RM.View.Editor;
using _1RM.View.Editor.Forms.AlternativeCredential;
using _1RM.View.Utils;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Stylet;

namespace _1RM.View.Settings.CredentialVault
{
    public class CredentialItem
    {
        private readonly DataSourceBase _dataSource;
        private readonly Credential _credential;

        public CredentialItem(DataSourceBase dataSource, Credential credential)
        {
            _dataSource = dataSource;
            _credential = credential;
        }

        public DataSourceBase DataSource => _dataSource;

        public Credential Credential => _credential;
    }

    public class CredentialVaultViewModel : NotifyPropertyChangedBase
    {
        private readonly DataSourceService _sourceService;

        private ObservableCollection<CredentialItem> _credentials = new ObservableCollection<CredentialItem>();
        public ObservableCollection<CredentialItem> Credentials
        {
            get => _credentials;
            set => SetAndNotifyIfChanged(ref _credentials, value);
        }

        public CredentialVaultViewModel(DataSourceService sourceService, GlobalData appData)
        {
            _sourceService = sourceService;
            OnDataReloaded();
            appData.OnDataReloaded += OnDataReloaded;
        }

        private void OnDataReloaded()
        {
            Execute.OnUIThreadSync(() =>
            {
                var tuples = _sourceService.GetSourceCredentials(false);
                foreach (var tuple in tuples)
                {
                    Credentials.Add(new CredentialItem(tuple.Item1, tuple.Item2));
                }
            });
        }


        private RelayCommand? _cmdAdd;
        public RelayCommand CmdAdd
        {
            get
            {
                return _cmdAdd ??= new RelayCommand(async (o) =>
                {
                    var source = await DataSourceSelectorViewModel.SelectDataSourceAsync();
                    if (source == null) return;
                    var existedNames = Credentials.Where(x => x.DataSource == source).Select(x => x.Credential.Name).ToList();
                    var vm = new AlternativeCredentialEditViewModel(existedNames, showHost: false)
                    {
                        RequireUserName = true,
                        RequirePassword = true,
                        RequirePrivateKey = true,
                    };
                    vm.OnSave += () =>
                    {
                        var ret = source.Database_InsertCredential(vm.New);
                        if (ret.IsSuccess)
                        {
                            Credentials.Add(new CredentialItem(source, vm.New));
                        }
                        else
                        {
                            MessageBoxHelper.ErrorAlert(ret.ErrorInfo);
                        }
                    };
                    MaskLayerController.ShowWindowWithMask(vm);
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
                    if (o is CredentialItem item)
                    {
                        var source = item.DataSource;
                        item.Credential.DecryptToConnectLevel();
                        var name = item.Credential.Name;
                        var existedNames = Credentials.Where(x => x != item).Select(x => x.Credential.Name).ToList();
                        var vm = new AlternativeCredentialEditViewModel(existedNames, org: item.Credential, showHost: false)
                        {
                            RequireUserName = true,
                            RequirePassword = true,
                            RequirePrivateKey = true,
                        };
                        vm.OnSave += () =>
                        {
                            // TODO: 编辑后，把所有引用这个 Credential 的 Protocol 都更新，使用事务
                            source.Database_UpdateCredential(vm.New, name);
                            var i = Credentials.IndexOf(item);
                            Credentials.Remove(item);
                            Credentials.Insert(i, new CredentialItem(source, vm.New));
                        };
                        MaskLayerController.ShowWindowWithMask(vm);
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
                    if (o is not CredentialItem item
                        || true != MessageBoxHelper.Confirm(IoC.Translate("confirm_to_delete_selected") + " -> " + item.Credential.Name))
                        return;

                    var ret = item.DataSource.Database_DeleteCredential(new[] { item.Credential.Name });
                    if (ret.IsSuccess)
                        Credentials.Remove(item);
                    else
                        MessageBoxHelper.ErrorAlert(ret.ErrorInfo);
                });
            }
        }
    }
}
