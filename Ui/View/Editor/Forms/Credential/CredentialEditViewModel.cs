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
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.Utils;
using _1RM.View.Settings.DataSource;
using com.github.xiangyuecn.rsacsharp;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.FileSystem;
using Stylet;

namespace _1RM.View.Editor.Forms.Credential
{
    public class CredentialEditViewModel : NotifyPropertyChangedBaseScreen
    {
        public CredentialWithAddressPortUserPwd? Org { get; } = null;
        public CredentialWithAddressPortUserPwd New { get; }= new CredentialWithAddressPortUserPwd();
        private readonly ProtocolBaseWithAddressPortUserPwd _protocol;
        public CredentialEditViewModel(ProtocolBaseWithAddressPortUserPwd protocol, CredentialWithAddressPortUserPwd? org = null)
        {
            _protocol = protocol;
            Org = org;

            // Edit mode
            if (Org != null)
            {
                Name = Org.Name;
            }
        }


        private string _name = "";
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    if (string.IsNullOrWhiteSpace(_name))
                    {
                        _name = "";
                        RaisePropertyChanged();
                        throw new ArgumentException(IoC.Get<ILanguageService>().Translate("Can not be empty!"));
                    }

                    if (true == _protocol.Credentials?.Any(x => x != Org && string.Equals(x.Name, _name, StringComparison.CurrentCultureIgnoreCase)))
                    {
                        _name = "";
                        RaisePropertyChanged();
                        throw new ArgumentException(IoC.Get<ILanguageService>().Translate("{0} is existed!", _name));
                    }

                    New.Name = value;
                    RaisePropertyChanged();
                }
            }
        }



        private RelayCommand? _cmdSave;
        public RelayCommand CmdSave
        {
            get
            {
                return _cmdSave ??= new RelayCommand((o) =>
                {
                    if (string.IsNullOrWhiteSpace(_name)
                        || true == _protocol.Credentials?.Any(x => x != Org && string.Equals(x.Name, _name, StringComparison.CurrentCultureIgnoreCase)))
                        return;
                    RequestClose(true);

                }, o => string.IsNullOrWhiteSpace(Name) == false);
            }
        }



        private RelayCommand? _cmdCancel;
        public RelayCommand CmdCancel
        {
            get
            {
                return _cmdCancel ??= new RelayCommand((o) =>
                {
                    RequestClose(false);
                });
            }
        }
    }
}
