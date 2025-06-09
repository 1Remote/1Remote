using _1RM.Model;
using _1RM.Model.Protocol.Base;
using _1RM.Utils;
using System.Collections.Generic;
using System.Linq;
using _1RM.Service.DataSource;
using System.Windows.Controls;
using System;
using _1RM.Service;
using System.Windows;
using Shawn.Utils.Wpf.FileSystem;
using _1RM.Model.Protocol;

namespace _1RM.View.Editor.Forms.AlternativeCredential;

public class CredentialViewModel : NotifyPropertyChangedBaseScreen
{
    public ProtocolBaseWithAddressPortUserPwd New { get; }
    public List<Credential> Credentials { get; }
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
                }
                else if (!string.IsNullOrEmpty(value.UserName) && !string.IsNullOrEmpty(value.PrivateKeyPath))
                {
                    New.UserName = value.UserName;
                    New.PrivateKey = value.PrivateKeyPath;
                }
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