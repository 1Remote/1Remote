using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using _1RM.Model.Protocol;
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
        public CredentialItem(DataSourceBase database, Credential credential)
        {
            Database = database;
            Credential = credential;
        }

        public DataSourceBase Database { get; }
        public Credential Credential { get; }
    }

    public class PasswordVaultViewModel : NotifyPropertyChangedBase
    {
        private DataSourceService _sourceService;

        public ObservableCollection<CredentialItem> Credentials { get; } = new ObservableCollection<CredentialItem>();
        public PasswordVaultViewModel(DataSourceService sourceService)
        {
            _sourceService = sourceService;
            // TODO Read from data sources
            var source = _sourceService.LocalDataSource;
            Credentials.Add(new CredentialItem(source, new Credential()
            {
                Name = "sd123123123123213123123123",
                UserName = "user121231231231231231232133",
                Password = "123dsdads",
            }));
            Credentials.Add(new CredentialItem(source, new Credential()
            {
                Name = "xxxxxsd123123123123213123123123",
                UserName = "user121231231231231231232133",
                Password = "0",
                PrivateKeyPath = "sadasdasdasdasd"
            }));
        }




        private RelayCommand? _cmdAdd;
        public RelayCommand CmdAdd
        {
            get
            {
                return _cmdAdd ??= new RelayCommand( async (o) =>
                {
                    var source = await DataSourceSelectorViewModel.SelectDataSourceAsync();
                    if(source == null) return;

                    var protocol = new RDP();
                    var existedNames = new List<string>();
                    var vm = new AlternativeCredentialEditViewModel(protocol, existedNames, showHost: false);
                    MaskLayerController.ShowWindowWithMask(vm);

                    var c = protocol.AlternateCredentials.FirstOrDefault();
                    if (c != null)
                    {
                        if (!string.IsNullOrEmpty(c.PrivateKeyPath))
                        {
                            c.Password = "";
                        }
                        else
                        {
                            c.PrivateKeyPath = "";
                        }
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
                    if (o is CredentialItem item)
                    {
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
                    if (o is CredentialItem item)
                    {
                        if (true == MessageBoxHelper.Confirm(IoC.Translate("confirm_to_delete_selected") + " -> " + item.Credential.Name))
                        {
                            //if (_configurationService.AdditionalDataSource.Contains(configBase))
                            //{
                            //    _configurationService.AdditionalDataSource.Remove(configBase);
                            //    _configurationService.Save();
                            //}
                        }
                    }
                });
            }
        }
    }
}
