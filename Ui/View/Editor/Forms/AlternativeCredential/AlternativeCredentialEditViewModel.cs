using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Windows;
using System.Xml.Linq;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.FileSystem;

namespace _1RM.View.Editor.Forms.AlternativeCredential
{
    public class AlternativeCredentialEditViewModel : NotifyPropertyChangedBaseScreen
    {
        private readonly Model.Protocol.Base.Credential? _org = null;
        public Model.Protocol.Base.Credential New { get; } = new Model.Protocol.Base.Credential();
        private readonly ProtocolBaseWithAddressPort _protocol;
        public AlternativeCredentialEditViewModel(ProtocolBaseWithAddressPort protocol, Model.Protocol.Base.Credential? org = null)
        {
            _protocol = protocol;
            _org = org;

            ShowUsername = true;
            ShowPassword = true;
            ShowPrivateKeyPath = false;

            if (protocol is VNC
                || protocol is not ProtocolBaseWithAddressPortUserPwd)
            {
                ShowUsername = false;
                ShowPassword = false;
            }

            if (protocol is SSH
                || protocol is SFTP)
            {
                ShowPrivateKeyPath = true;
            }

            // Edit mode
            if (_org != null)
            {
                if (string.IsNullOrEmpty(_org.PrivateKeyPath) == false)
                {
                    IsUsePrivateKey = true;
                }
                New = (Model.Protocol.Base.Credential)_org.Clone();
            }
        }

        public bool ShowUsername { get; }
        public bool ShowPassword { get; }
        public bool ShowPrivateKeyPath { get; }

        private bool _isUsePrivateKey = false;
        public bool IsUsePrivateKey
        {
            get => _isUsePrivateKey && ShowPrivateKeyPath;
            set
            {
                if (SetAndNotifyIfChanged(ref _isUsePrivateKey, value))
                {
                    if (value)
                    {
                        New.Password = "";
                    }
                    else
                    {
                        New.PrivateKeyPath = "";
                    }
                }
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
                        throw new ArgumentException(IoC.Get<ILanguageService>().Translate("XXX is already existed!", v));
                    }

                    New.Name = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string PrivateKeyPath
        {
            get => New.PrivateKeyPath;
            set
            {
                var v = value.Trim();
                if (New.PrivateKeyPath != v)
                {
                    if (string.IsNullOrWhiteSpace(v))
                    {
                        New.PrivateKeyPath = "";
                        RaisePropertyChanged();
                    }
                    else
                    {
                        New.PrivateKeyPath = v;
                        RaisePropertyChanged();
                        if (File.Exists(New.PrivateKeyPath) == false)
                        {
                            throw new ArgumentException(IoC.Get<ILanguageService>().Translate("XXX is not existed!", v));
                        }
                    }
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
                        MessageBoxHelper.Warning(IoC.Get<ILanguageService>().Translate("XXX is already existed!", Name));
                        return;
                    }

                    if (!ShowUsername)
                        New.UserName = "";
                    if (!ShowPassword || _isUsePrivateKey)
                        New.Password = "";
                    if (!ShowPrivateKeyPath || !_isUsePrivateKey)
                        New.PrivateKeyPath = "";

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


        public void ButtonOpenPrivateKey_OnClick(object sender, RoutedEventArgs e)
        {
            var path = SelectFileHelper.OpenFile(filter: "ppk|*.*", currentDirectoryForShowingRelativePath: Environment.CurrentDirectory);
            if (path == null) return;
            PrivateKeyPath = path;
        }
    }
}
