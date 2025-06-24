using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
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
        public Model.Protocol.Base.Credential New { get; } = new Model.Protocol.Base.Credential();
        private readonly List<string>? _existedNames;
        public Func<bool>? OnSave { get; set; }
        public bool RequireHost = false;
        public bool RequirePort = false;
        public bool RequireUserName = false;
        public bool RequirePassword = false;
        public bool RequirePrivateKey = false;


        public static AlternativeCredentialEditViewModel NewFormProtocol(ProtocolBaseWithAddressPort protocol, List<string>? existedNames, Model.Protocol.Base.Credential? org = null, bool showHost = true)
        {
            bool showUsername = true;
            bool showPassword = true;
            bool showPrivateKeyPath = false;

            if (protocol is not ProtocolBaseWithAddressPortUserPwd pp)
            {
                showUsername = false;
                showPassword = false;
                showPrivateKeyPath = false;
            }
            else
            {
                showUsername = pp.ShowUserNameInput();
                showPassword = pp.ShowPasswordInput();
                showPrivateKeyPath = pp.ShowPrivateKeyInput();
            }
            return new AlternativeCredentialEditViewModel(existedNames, org, showHost, showUsername, showPassword, showPrivateKeyPath);
        }

        /// <summary>
        /// </summary>
        /// <param name="existedNames">current protocol's existed names, used to check if the name is already existed</param>
        /// <param name="org">the original credential, if null, it means a new credential</param>
        /// <param name="showHost"> if true, show host input and port input, otherwise hide it</param>
        public AlternativeCredentialEditViewModel(List<string>? existedNames, Model.Protocol.Base.Credential? org = null, bool showHost = true, bool showUsername = true, bool showPassword = true, bool showPrivateKey = true)
        {
            _existedNames = existedNames;
            if (org != null && _existedNames?.Contains(org.Name) == true)
                _existedNames.Remove(org.Name);

            ShowHost = showHost;
            ShowUsername = showUsername;
            ShowPassword = showPassword;
            ShowPrivateKeyInput = showPrivateKey;

            if (org != null)
            {
                if (string.IsNullOrEmpty(org.UserName) == false)
                {
                    ShowUsername = true;
                }
                if (string.IsNullOrEmpty(org.Password) == false)
                {
                    ShowPassword = true;
                }
                if (string.IsNullOrEmpty(org.PrivateKeyPath) == false)
                {
                    ShowPrivateKeyInput = true;
                }
            }

            if (!ShowPassword && ShowPrivateKeyInput)
            {
                IsUsePrivateKey = true;
            }

            // Edit mode
            if (org != null)
            {
                if (string.IsNullOrEmpty(org.PrivateKeyPath) == false)
                {
                    IsUsePrivateKey = true;
                }
                New = (Model.Protocol.Base.Credential)org.Clone();
            }
        }

        public bool ShowHost { get; }
        public bool ShowUsername { get; }
        public bool ShowPassword { get; }
        public bool ShowPrivateKeyInput { get; }

        private bool _isUsePrivateKey = false;
        public bool IsUsePrivateKey
        {
            get => _isUsePrivateKey && ShowPrivateKeyInput;
            set
            {
                if (!ShowPassword && ShowPrivateKeyInput)
                {
                    _isUsePrivateKey = true;
                    New.Password = "";
                    RaisePropertyChanged();
                    return;
                }
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
                var v = value;
                if (New.Name != v)
                {
                    New.Name = v;
                    RaisePropertyChanged();
                }
            }
        }

        public string Address
        {
            get => New.Address;
            set
            {
                var v = value;
                if (New.Address != v)
                {
                    New.Address = v;
                    RaisePropertyChanged();
                }
            }
        }

        public string Port
        {
            get => New.Port;
            set
            {
                var v = value;
                if (New.Port != v && int.TryParse(v, out _))
                {
                    New.Port = v;
                    RaisePropertyChanged();
                }
            }
        }

        public string UserName
        {
            get => New.UserName;
            set
            {
                New.UserName = value.Trim();
                RaisePropertyChanged();
            }
        }

        public string Password
        {
            get => New.Password;
            set
            {
                New.Password = value;
                RaisePropertyChanged();
            }
        }

        public string PrivateKeyPath
        {
            get => New.PrivateKeyPath;
            set
            {
                New.PrivateKeyPath = value.Trim();
                RaisePropertyChanged();
            }
        }

        private RelayCommand? _cmdSave;
        public RelayCommand CmdSave
        {
            get
            {
                return _cmdSave ??= new RelayCommand((_) =>
                {
                    if (!ShowUsername)
                        New.UserName = "";
                    if (!ShowPassword || _isUsePrivateKey)
                        New.Password = "";
                    if (!ShowPrivateKeyInput || !_isUsePrivateKey)
                        New.PrivateKeyPath = "";
                    New.Trim();
                    if (OnSave?.Invoke() == true)
                        RequestClose(true);
                }, o => CanSave());
            }
        }

        public bool CanSave()
        {
            if (!string.IsNullOrEmpty(this[nameof(Name)])
                || !string.IsNullOrEmpty(this[nameof(Address)])
                || !string.IsNullOrEmpty(this[nameof(Port)])
                || !string.IsNullOrEmpty(this[nameof(UserName)])
                || !string.IsNullOrEmpty(this[nameof(Password)])
                || !string.IsNullOrEmpty(this[nameof(PrivateKeyPath)])
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
                            if (_existedNames?.Any(x => string.Equals(x, Name.Trim(), StringComparison.CurrentCultureIgnoreCase)) == true)
                                return IoC.Translate(LanguageService.XXX_IS_ALREADY_EXISTED, Name.Trim());
                            if (Name.Length > 100)
                                return "too long";
                            break;
                        }
                    case nameof(Address):
                        {
                            if (ShowHost && RequireHost && string.IsNullOrWhiteSpace(Address))
                                return IoC.Translate(LanguageService.CAN_NOT_BE_EMPTY);
                            break;
                        }
                    case nameof(Port):
                        {
                            if (!string.IsNullOrWhiteSpace(Port) && (int.TryParse(Port, out var p) == false || p < 0 || p > 65535))
                                return "1 - 65535";
                            break;
                        }
                    case nameof(UserName):
                        {
                            if (ShowUsername && RequireUserName && string.IsNullOrWhiteSpace(UserName))
                                return IoC.Translate(LanguageService.CAN_NOT_BE_EMPTY);
                            break;
                        }
                    case nameof(Password):
                        {
                            if (ShowPassword && RequirePassword && !IsUsePrivateKey && string.IsNullOrWhiteSpace(Password))
                                return IoC.Translate(LanguageService.CAN_NOT_BE_EMPTY);
                            break;
                        }
                    case nameof(PrivateKeyPath):
                        {
                            if (ShowPrivateKeyInput && RequirePrivateKey && IsUsePrivateKey && string.IsNullOrWhiteSpace(PrivateKeyPath))
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
