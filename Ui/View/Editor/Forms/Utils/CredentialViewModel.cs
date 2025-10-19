using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.Utils;
using _1RM.View.Editor.Forms.AlternativeCredential;
using _1RM.View.Utils;
using Shawn.Utils.Wpf;
using static Microsoft.WindowsAPICodePack.Shell.PropertySystem.SystemProperties.System;

namespace _1RM.View.Editor.Forms.Utils;

public class CredentialViewModel : NotifyPropertyChangedBaseScreen
{
    public ProtocolBaseWithAddressPortUserPwd New { get; }
    public bool ShowPrivateKeyInput { get; }

    private Credential? _manuallyCache;
    private string _credentialNameCache = "";

    public CredentialViewModel(ProtocolBaseWithAddressPortUserPwd protocol)
    {
        New = protocol;
        var credentials = protocol.DataSource?.GetCredentials(true)?.ToList() ?? new List<Credential>();
        if (credentials.Any())
        {
            if (protocol.ShowPasswordInput() && protocol.ShowPrivateKeyInput())
            {
            }

            if (protocol.ShowPasswordInput() && !protocol.ShowPrivateKeyInput())
            {
                credentials = credentials.Where(x => !string.IsNullOrEmpty(x.Password)).ToList();
            }

            if (!protocol.ShowPasswordInput() && protocol.ShowPrivateKeyInput())
            {
                credentials = credentials.Where(x => !string.IsNullOrEmpty(x.PrivateKeyPath)).ToList();
            }
        }

        ShowPrivateKeyInput = protocol.ShowPrivateKeyInput();
        if (!ShowPrivateKeyInput)
        {
            New.UsePrivateKeyForConnect = false; // force to use password for connect, because private key is not supported in this form
            New.PrivateKey = "";
        }
        IsUsePrivateKey = New.UsePrivateKeyForConnect;

        _credentialNameCache = protocol.InheritedCredentialName ?? "";
        _manuallyCache = protocol.GetCredential();

        StrBtnUseCredentialsVault = IoC.Translate("Credentials Vault");
        if (string.IsNullOrEmpty(protocol.InheritedCredentialName))
        {
            SelectedCredential = null;
        }
        else if (protocol.InheritedCredentialName == protocol.ServerEditorDifferentOptions)
        {
            // bulk edit mode, show "Different Options" option
            StrBtnUseCredentialsVault = protocol.ServerEditorDifferentOptions;
            credentials.Insert(0, new Credential()
            {
                Name = protocol.ServerEditorDifferentOptions,
                UserName = protocol.UserName,
                Password = protocol.Password,
                PrivateKeyPath = protocol.PrivateKey
            });
            SelectedCredential = credentials.First();
        }
        else
        {
            SelectedCredential = credentials.FirstOrDefault(x => x.Name == protocol.InheritedCredentialName);
        }
        Credentials = credentials;
    }

    private string _strBtnUseCredentialsVault;
    public string StrBtnUseCredentialsVault
    {
        get => _strBtnUseCredentialsVault;
        set => SetAndNotifyIfChanged(ref _strBtnUseCredentialsVault, value);
    }


    public List<Credential> Credentials { get; set; }


    private Credential? _selectedCredential = null;
    public Credential? SelectedCredential
    {
        get => _selectedCredential;
        set
        {
            if (!SetAndNotifyIfChanged(ref _selectedCredential, value)) return;
            New.InheritedCredentialName = value?.Name ?? "";
            if (value != null)
            {
                value.DecryptToConnectLevel();
                if (!string.IsNullOrEmpty(value.UserName) && !string.IsNullOrEmpty(value.Password))
                {
                    New.UserName = value.UserName;
                    New.Password = value.Password;
                    New.PrivateKey = "";
                }
                else if (!string.IsNullOrEmpty(value.UserName) && !string.IsNullOrEmpty(value.PrivateKeyPath))
                {
                    New.UserName = value.UserName;
                    New.Password = "";
                    New.PrivateKey = value.PrivateKeyPath;
                }

                if (New.PrivateKey == New.ServerEditorDifferentOptions)
                {
                    New.UsePrivateKeyForConnect = null; // force to use password for connect, because private key is not supported in this form
                }
                else
                {
                    New.UsePrivateKeyForConnect = !string.IsNullOrEmpty(New.PrivateKey);
                }
            }
            RaisePropertyChanged(nameof(SelectedCredentialName));
        }
    }

    public string SelectedCredentialName
    {
        get => _selectedCredential?.Name ?? "";
        set
        {
            if (_selectedCredential?.Name != value)
            {
                SelectedCredential = Credentials.FirstOrDefault(x => x.Name == value) ?? Credentials.First();
            }
        }
    }

    private bool? _isUsePrivateKey = false;
    public bool? IsUsePrivateKey
    {
        get
        {
            if (_isUsePrivateKey == null) return _isUsePrivateKey;
            return (_isUsePrivateKey ?? false) && ShowPrivateKeyInput;
        }
        set
        {
            if (SetAndNotifyIfChanged(ref _isUsePrivateKey, value))
            {
                if (value == true)
                {
                    New.Password = "";
                }
                else if (value == false)
                {
                    New.PrivateKey = "";
                }
                New.UsePrivateKeyForConnect = value;
            }
        }
    }


    public void ButtonSelectPrivateKey_OnClick(object sender, RoutedEventArgs e)
    {
        var path = SelectFileHelper.OpenFile(filter: "ppk|*.*", currentDirectoryForShowingRelativePath: Environment.CurrentDirectory, owner: View as Window);
        if (path == null) return;
        New.PrivateKey = path;
        New.Password = "";
    }




    private RelayCommand? _cmdAdd;
    public RelayCommand CmdAdd
    {
        get
        {
            return _cmdAdd ??= new RelayCommand(async (o) =>
            {
                var source = New.DataSource;
                if (source == null) return;
                var existedNames = Credentials.Where(x => x.DataSource == source).Select(x => x.Name).ToList();
                var vm = new AlternativeCredentialEditViewModel(existedNames, showHost: false, title: IoC.Translate("Add") + " " + IoC.Translate("Credentials"))
                {
                    RequireUserName = New.ShowUserNameInput(),
                    RequirePassword = New.ShowPasswordInput(),
                    RequirePrivateKey = New.ShowPrivateKeyInput(),
                };
                vm.OnSave += () =>
                {
                    var ret = source.Database_InsertCredential(vm.New);
                    if (ret.IsSuccess)
                    {
                        Credentials.Add(vm.New);
                        SelectedCredential = vm.New;
                        RaisePropertyChanged(nameof(Credentials));
                        return true;
                    }
                    else
                    {
                        MessageBoxHelper.ErrorAlert(ret.ErrorInfo);
                        return false; // do not close the dialog
                    }
                };
                MaskLayerController.ShowWindowWithMask(vm);
            }, o => New.DataSource != null); // when DataSource is null, it means in bulk edit and servers from different database are editing.
        }
    }




    private RelayCommand? _cmdUseManuallyCredential;
    public RelayCommand CmdUseManuallyCredential
    {
        get
        {
            return _cmdUseManuallyCredential ??= new RelayCommand(async (o) =>
            {
                if (_manuallyCache != null)
                {
                    _credentialNameCache = SelectedCredential?.Name ?? "";
                    SelectedCredential = null;
                    _manuallyCache.Address = "";
                    _manuallyCache.Port = "";
                    New.SetCredential(_manuallyCache, true);
                }
            });
        }
    }

    private RelayCommand? _cmdUseCredentialsVault;
    public RelayCommand CmdUseCredentialsVault
    {
        get
        {
            return _cmdUseCredentialsVault ??= new RelayCommand(async (o) =>
            {
                if (Credentials.Count == 0)
                {
                    // if nothing in the vault, add first
                    CmdAdd.Execute(o);
                }
                _manuallyCache = New.GetCredential();
                if (Credentials.Count != 0)
                {
                    _manuallyCache = New.GetCredential();
                    IsUsePrivateKey = !string.IsNullOrEmpty(SelectedCredential?.PrivateKeyPath);
                    SelectedCredential = Credentials.FirstOrDefault(x => x.Name == _credentialNameCache)
                                         ?? Credentials.FirstOrDefault(x => x.Name == New.ServerEditorDifferentOptions)
                                         ?? Credentials.FirstOrDefault();
                }
            });
        }
    }
}