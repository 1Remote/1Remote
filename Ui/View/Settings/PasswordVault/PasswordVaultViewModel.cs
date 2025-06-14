using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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

namespace _1RM.View.Settings.PasswordVault
{
    public class CredentialItem
    {
        public CredentialItem(DataSourceBase dataSource, Credential credential)
        {
            DataSource = dataSource;
            Credential = credential;
        }

        public DataSourceBase DataSource { get; }
        public Credential Credential { get; }
    }

    public class PasswordVaultViewModel : NotifyPropertyChangedBase
    {
        private readonly DataSourceService _sourceService;

        public ObservableCollection<CredentialItem> Credentials { get; } = new ObservableCollection<CredentialItem>();
        public PasswordVaultViewModel(DataSourceService sourceService)
        {
            _sourceService = sourceService;
            // TODO Read from data sources
            var tuples = _sourceService.GetSourceCredentials(false);
            foreach (var tuple in tuples)
            {
                Credentials.Add(new CredentialItem(tuple.Item1, tuple.Item2));
            }
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
                        source.Database_InsertCredential(vm.New);
                        Credentials.Add(new CredentialItem(source, vm.New));
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
                            source.Database_UpdateCredential(vm.New);
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
