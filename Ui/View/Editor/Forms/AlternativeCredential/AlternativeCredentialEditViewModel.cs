using System;
using System.Linq;
using _1RM.Model.Protocol.Base;
using _1RM.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;

namespace _1RM.View.Editor.Forms.AlternativeCredential
{
    public class AlternativeCredentialEditViewModel : NotifyPropertyChangedBaseScreen
    {
        public CredentialWithAddressPortUserPwd? Org { get; } = null;
        public CredentialWithAddressPortUserPwd New { get; }= new CredentialWithAddressPortUserPwd();
        private readonly ProtocolBaseWithAddressPortUserPwd _protocol;
        public AlternativeCredentialEditViewModel(ProtocolBaseWithAddressPortUserPwd protocol, CredentialWithAddressPortUserPwd? org = null)
        {
            _protocol = protocol;
            Org = org;

            // Edit mode
            if (Org != null)
            {
                Name = Org.Name;
            }
        }


        public string Name
        {
            get => New.Name;
            set
            {
                if (New.Name != value)
                {
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        New.Name = "";
                        RaisePropertyChanged();
                        throw new ArgumentException(IoC.Get<ILanguageService>().Translate("Can not be empty!"));
                    }

                    if (true == _protocol.Credentials?.Any(x => x != Org && string.Equals(x.Name, value, StringComparison.CurrentCultureIgnoreCase)))
                    {
                        New.Name = "";
                        RaisePropertyChanged();
                        throw new ArgumentException(IoC.Get<ILanguageService>().Translate("{0} is existed!", value));
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
                    if (string.IsNullOrWhiteSpace(Name)
                        || true == _protocol.Credentials?.Any(x => x != Org && string.Equals(x.Name, Name, StringComparison.CurrentCultureIgnoreCase)))
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
