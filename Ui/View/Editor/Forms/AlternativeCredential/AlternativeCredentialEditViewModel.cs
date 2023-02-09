using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml.Linq;
using _1RM.Model.Protocol.Base;
using _1RM.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;

namespace _1RM.View.Editor.Forms.AlternativeCredential
{
    public class AlternativeCredentialEditViewModel : NotifyPropertyChangedBaseScreen
    {
        private readonly CredentialWithAddressPortUserPwd? _org = null;
        public CredentialWithAddressPortUserPwd New { get; } = new CredentialWithAddressPortUserPwd();
        private readonly ProtocolBaseWithAddressPortUserPwd _protocol;
        public AlternativeCredentialEditViewModel(ProtocolBaseWithAddressPortUserPwd protocol, CredentialWithAddressPortUserPwd? org = null)
        {
            _protocol = protocol;
            _org = org;

            // Edit mode
            if (_org != null)
            {
                New = (CredentialWithAddressPortUserPwd)_org.Clone();
            }
        }


        public string Name
        {
            get => New.Name;
            set
            {
                var v = value.Trim();
                if (New.Name != v)
                {
                    if (string.IsNullOrWhiteSpace(v))
                    {
                        New.Name = "";
                        RaisePropertyChanged();
                        throw new ArgumentException(IoC.Get<ILanguageService>().Translate("Can not be empty!"));
                    }

                    if (true == _protocol.Credentials?.Any(x => x != _org && string.Equals(x.Name, v, StringComparison.CurrentCultureIgnoreCase)))
                    {
                        New.Name = "";
                        RaisePropertyChanged();
                        throw new ArgumentException(IoC.Get<ILanguageService>().Translate("{0} is existed!", v));
                    }

                    New.Name = value;
                    RaisePropertyChanged();
                }
            }
        }

        private bool CheckPort(string port)
        {
            if (string.IsNullOrWhiteSpace(port))
                return true;

            if (int.TryParse(port, out var i)
                && i is > 0 and < 65536)
            {
                return true;
            }
            return false;
        }

        public string Port
        {
            get => New.Port;
            set
            {
                var v = value.Trim();
                if (New.Port != v)
                {
                    New.Port = v;
                    RaisePropertyChanged();
                    if (!CheckPort(v))
                    {
                        throw new ArgumentException();
                    }
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
                    _protocol.Credentials ??= new ObservableCollection<CredentialWithAddressPortUserPwd>();

                    if (string.IsNullOrWhiteSpace(Name))
                    {
                        MessageBoxHelper.Warning($"`{IoC.Get<ILanguageService>().Translate("Name")}` {IoC.Get<ILanguageService>().Translate("Can not be empty!")}");
                        return;
                    }
                    if (true == _protocol.Credentials.Any(x => x != _org && string.Equals(x.Name, Name, StringComparison.CurrentCultureIgnoreCase)))
                    {
                        MessageBoxHelper.Warning(IoC.Get<ILanguageService>().Translate("{0} is existed!", Name));
                        return;
                    }

                    if (_org != null && _protocol.Credentials.Any(x => x == _org))
                    {
                        // edit
                        var i = _protocol.Credentials.IndexOf(_org);
                        _protocol.Credentials.Remove(_org);
                        _protocol.Credentials.Insert(i, New);
                    }
                    else
                    {
                        // add
                        _protocol.Credentials.Add(New);
                    }
                    RequestClose(true);
                }, o => string.IsNullOrWhiteSpace(Name) == false && CheckPort(Port));
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
