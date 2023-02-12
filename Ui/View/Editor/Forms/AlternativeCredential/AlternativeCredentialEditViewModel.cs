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
        private readonly Model.Protocol.Base.Credential? _org = null;
        public Model.Protocol.Base.Credential New { get; } = new Model.Protocol.Base.Credential();
        private readonly ProtocolBaseWithAddressPortUserPwd _protocol;
        public AlternativeCredentialEditViewModel(ProtocolBaseWithAddressPortUserPwd protocol, Model.Protocol.Base.Credential? org = null)
        {
            _protocol = protocol;
            _org = org;

            // Edit mode
            if (_org != null)
            {
                New = (Model.Protocol.Base.Credential)_org.Clone();
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

                    if (true == _protocol.AlternateCredentials?.Any(x => x != _org && string.Equals(x.Name, v, StringComparison.CurrentCultureIgnoreCase)))
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
                    _protocol.AlternateCredentials ??= new ObservableCollection<Model.Protocol.Base.Credential>();

                    if (string.IsNullOrWhiteSpace(Name))
                    {
                        MessageBoxHelper.Warning($"`{IoC.Get<ILanguageService>().Translate("Name")}` {IoC.Get<ILanguageService>().Translate("Can not be empty!")}");
                        return;
                    }
                    if (true == _protocol.AlternateCredentials.Any(x => x != _org && string.Equals(x.Name, Name, StringComparison.CurrentCultureIgnoreCase)))
                    {
                        MessageBoxHelper.Warning(IoC.Get<ILanguageService>().Translate("{0} is existed!", Name));
                        return;
                    }

                    if (_org != null && _protocol.AlternateCredentials.Any(x => x == _org))
                    {
                        // edit
                        var i = _protocol.AlternateCredentials.IndexOf(_org);
                        _protocol.AlternateCredentials.Remove(_org);
                        _protocol.AlternateCredentials.Insert(i, New);
                    }
                    else
                    {
                        // add
                        _protocol.AlternateCredentials.Add(New);
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
