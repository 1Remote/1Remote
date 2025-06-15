using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Utils;
using Shawn.Utils.Wpf.FileSystem;

namespace _1RM.View.Editor.Forms.Utils;

public class CredentialViewModel : NotifyPropertyChangedBaseScreen
{
    public ProtocolBaseWithAddressPortUserPwd New { get; }
    public List<Credential> Credentials { get; }
    public List<string> CredentialNames { get; }
    public bool ShowPrivateKeyInput { get; }
    public CredentialViewModel(ProtocolBaseWithAddressPortUserPwd protocol)
    {
        New = protocol;
        var credentials = IoC.Get<DataSourceService>().GetCredentials(true);
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

    private Credential _selectedCredential;
    public Credential SelectedCredential
    {
        get => _selectedCredential;
        set
        {
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
        get => _selectedCredential.Name;
        set
        {
            if (_selectedCredential.Name != value)
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
}