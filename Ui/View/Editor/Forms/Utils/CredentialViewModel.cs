using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.Utils;
using _1RM.View.Editor.Forms.AlternativeCredential;
using _1RM.View.Settings.CredentialVault;
using _1RM.View.Utils;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.FileSystem;

namespace _1RM.View.Editor.Forms.Utils;

public class CredentialViewModel : NotifyPropertyChangedBaseScreen
{
    public ProtocolBaseWithAddressPortUserPwd New { get; }
    public List<Credential> Credentials { get; private set; }
    public List<string> CredentialNames { get; }
    public bool ShowPrivateKeyInput { get; }
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
        if (New.UsePrivateKeyForConnect == true)
        {
            New.UsePrivateKeyForConnect = false; // force to use password for connect, because private key is not supported in this form
        }

        Credentials = new List<Credential>() { new Credential() };
        if(protocol.InheritedCredentialName == protocol.ServerEditorDifferentOptions)
        {
            // bulk edit mode, show "Different Options" option
            Credentials.Add(new Credential()
            {
                Name = protocol.ServerEditorDifferentOptions,
                UserName = protocol.UserName,
                Password = protocol.Password,
                PrivateKeyPath = protocol.PrivateKey
            });
        }
        Credentials.AddRange(credentials);
        CredentialNames = Credentials.Select(x => x.Name).Distinct().ToList();
        var selected = Credentials.FirstOrDefault(x => x.Name == protocol.InheritedCredentialName);
        _selectedCredential = selected ?? Credentials.First();
    }


    private Credential? _selectedCredential = null;
    public Credential? SelectedCredential
    {
        get => _selectedCredential;
        set
        {
            if (value == null) return;
            if (SetAndNotifyIfChanged(ref _selectedCredential, value))
            {
                New.InheritedCredentialName = value.Name;
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
                RaisePropertyChanged(nameof(SelectedCredentialName));
            }
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


    public void ButtonSelectPrivateKey_OnClick(object sender, RoutedEventArgs e)
    {
        var path = SelectFileHelper.OpenFile(filter: "ppk|*.*", currentDirectoryForShowingRelativePath: Environment.CurrentDirectory);
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
                var vm = new AlternativeCredentialEditViewModel(existedNames, showHost: false)
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
                        Credentials = new List<Credential>(Credentials);
                        RaisePropertyChanged(nameof(Credentials));
                    }
                    else
                    {
                        MessageBoxHelper.ErrorAlert(ret.ErrorInfo);
                    }
                };
                MaskLayerController.ShowWindowWithMask(vm);
            }, o => New.DataSource != null); // when DataSource is null, it means in bulk edit and servers from different database are editing.
        }
    }
}