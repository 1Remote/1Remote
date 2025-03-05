using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Xml.Linq;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.Utils;
using _1RM.View.Utils.MaskAndPop;
using Newtonsoft.Json;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.FileSystem;

namespace _1RM.View.Editor.Forms.AlternativeCredential
{
    public class AlternativeCredentialEditViewModel : PopupBase, IDataErrorInfo
    {
        private readonly Model.Protocol.Base.Credential? _org = null;
        public Model.Protocol.Base.Credential New { get; } = new Model.Protocol.Base.Credential();
        private readonly ProtocolBaseWithAddressPort _protocol;
        private readonly List<string>? _existedNames;
        public AlternativeCredentialEditViewModel(ProtocolBaseWithAddressPort protocol, List<string>? existedNames, Model.Protocol.Base.Credential? org = null)
        {
            _protocol = protocol;
            _org = org;
            _existedNames = existedNames;

            if (_org != null && _existedNames?.Contains(_org.Name) == true)
                _existedNames.Remove(_org.Name);

            ShowUsername = true;
            ShowPassword = true;
            ShowPrivateKeyPath = false;

            if (protocol is not ProtocolBaseWithAddressPortUserPwd pp)
            {
                ShowUsername = false;
                ShowPassword = false;
                ShowPrivateKeyPath = false;
            }
            else
            {
                ShowUsername = pp.ShowUserNameInput();
                ShowPassword = pp.ShowPasswordInput();
                ShowPrivateKeyPath = pp.ShowPrivateKeyInput();
            }

            if (_org != null)
            {
                if (string.IsNullOrEmpty(_org.UserName) == false)
                {
                    ShowUsername = true;
                }
                if (string.IsNullOrEmpty(_org.Password) == false)
                {
                    ShowPassword = true;
                }
                if (string.IsNullOrEmpty(_org.PrivateKeyPath) == false)
                {
                    ShowPrivateKeyPath = true;
                }
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
                        PrivateKeyPath = "";
                    }
                }
            }
        }


        public string Name
        {
            get => New.Name;
            set
            {
                if (New.Name != value)
                {
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
                New.PrivateKeyPath = value;
                RaisePropertyChanged();
            }
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
                }
            }
        }


        private RelayCommand? _cmdSave;
        public RelayCommand CmdSave
        {
            get
            {
                return _cmdSave ??= new RelayCommand((_) =>
                {
                    _protocol.AlternateCredentials ??= new ObservableCollection<Model.Protocol.Base.Credential>();

                    if (!ShowUsername)
                        New.UserName = "";
                    if (!ShowPassword || _isUsePrivateKey)
                        New.Password = "";
                    if (!ShowPrivateKeyPath || !_isUsePrivateKey)
                        New.PrivateKeyPath = "";


                    New.Trim();
                    if (_org != null && _protocol.AlternateCredentials.Any(x => x.Equals(_org)))
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
                }, o => CanSave());
            }
        }

        public bool CanSave()
        {
            if (!string.IsNullOrEmpty(this[nameof(Port)])
                || !string.IsNullOrEmpty(this[nameof(Name)]
                )
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
                        if (string.IsNullOrWhiteSpace(Name))
                            return IoC.Translate(LanguageService.CAN_NOT_BE_EMPTY);
                        if (_existedNames?.Any(x => string.Equals(x, Name, StringComparison.CurrentCultureIgnoreCase)) == true)
                            return IoC.Translate(LanguageService.XXX_IS_ALREADY_EXISTED, Name);
                        break;
                        }
                    case nameof(Port):
                    {
                        if (!string.IsNullOrWhiteSpace(Port) && (int.TryParse(Port, out var p) == false || p < 0 || p > 65535))
                            return "1 - 65535";
                        break;
                    }
                }
                return "";
            }
        }
        #endregion
    }
}
