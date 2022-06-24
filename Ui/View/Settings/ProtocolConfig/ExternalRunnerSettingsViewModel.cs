using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Animation;
using Newtonsoft.Json;
using PRM.Model.Protocol;
using PRM.Model.ProtocolRunner;
using PRM.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.Controls;
using Shawn.Utils.Wpf.FileSystem;
using Shawn.Utils.Wpf.PageHost;
using Stylet;

namespace PRM.View.Settings.ProtocolConfig;

public class ExternalRunnerSettingsViewModel
{
    private readonly ILanguageService _languageService;
    public ExternalRunner ExternalRunner { get; }

    public ExternalRunnerSettingsViewModel(ExternalRunner externalRunner, ILanguageService languageService)
    {
        ExternalRunner = externalRunner;
        _languageService = languageService;
    }


    private RelayCommand? _cmdSelectDbPath;
    [JsonIgnore]
    public RelayCommand CmdSelectExePath
    {
        get
        {
            return _cmdSelectDbPath ??= new RelayCommand((o) =>
            {
                string? initPath = null;
                try
                {
                    initPath = new FileInfo(ExternalRunner.ExePath).DirectoryName;
                }
                catch
                {
                    // ignored
                }


                var path = SelectFileHelper.OpenFile(filter: "exe|*.exe", checkFileExists: true, initialDirectory: initPath);
                if (path == null) return;
                ExternalRunner.ExePath = path;
                if (string.IsNullOrEmpty(ExternalRunner.Arguments))
                {
                    var name = new FileInfo(path).Name.ToLower();
                    if (name == "winscp.exe".ToLower())
                    {
                        if (ExternalRunner.OwnerProtocolName == SFTP.ProtocolName)
                        {
                            ExternalRunner.Arguments = "sftp://%PRM_USERNAME%:%PRM_PASSWORD%@%PRM_HOSTNAME%:%PRM_PORT%";
                        }
                        if (ExternalRunner.OwnerProtocolName == FTP.ProtocolName)
                        {
                            ExternalRunner.Arguments = "ftp://%PRM_USERNAME%:%PRM_PASSWORD%@%PRM_HOSTNAME%:%PRM_PORT%";
                        }
                        ExternalRunner.RunWithHosting = true;
                    }
                    else if (name == "filezilla.exe".ToLower() || path.ToLower().IndexOf("uvnc", StringComparison.Ordinal) > 0)
                    {
                        if (ExternalRunner.OwnerProtocolName == SFTP.ProtocolName)
                        {
                            ExternalRunner.Arguments = "sftp://%PRM_USERNAME%:%PRM_PASSWORD%@%PRM_HOSTNAME%";
                        }
                        if (ExternalRunner.OwnerProtocolName == FTP.ProtocolName)
                        {
                            ExternalRunner.Arguments = "ftp://%PRM_USERNAME%:%PRM_PASSWORD%@%PRM_HOSTNAME%";
                        }
                        ExternalRunner.Arguments = @"%PRM_HOSTNAME%::%PRM_PORT% -password=%PRM_PASSWORD% -scale=auto";
                        ExternalRunner.RunWithHosting = false;
                    }
                    else if (name == "VpxClient.exe".ToLower())
                    {
                        ExternalRunner.Arguments = @"-s %PRM_HOSTNAME% -u %PRM_USERNAME% -p %PRM_PASSWORD%";
                        ExternalRunner.RunWithHosting = true;
                    }
                    else if (name.IndexOf("kitty", StringComparison.Ordinal) >= 0 || name.IndexOf("putty", StringComparison.Ordinal) >= 0)
                    {
                        ExternalRunner.Arguments = @"-ssh %PRM_HOSTNAME% -P %PRM_PORT% -l %PRM_USERNAME% -pw %PRM_PASSWORD% -%PRM_SSH_VERSION% -cmd ""%PRM_STARTUP_AUTO_COMMAND%""";
                        ExternalRunner.RunWithHosting = true;
                    }
                    else if (name == "tvnviewer.exe".ToLower())
                    {
                        ExternalRunner.Arguments = @"%PRM_HOSTNAME%::%PRM_PORT% -password=%PRM_PASSWORD% -scale=auto";
                        ExternalRunner.RunWithHosting = true;
                    }
                    else if (name == "vncviewer.exe".ToLower() || path.ToLower().IndexOf("uvnc", StringComparison.Ordinal) > 0)
                    {
                        ExternalRunner.Arguments = @"%PRM_HOSTNAME%::%PRM_PORT% -password=%PRM_PASSWORD% -scale=auto";
                        ExternalRunner.RunWithHosting = false;
                    }
                }
            });
        }
    }


    private RelayCommand? _cmdAddEnvironmentVariable;
    public RelayCommand CmdAddEnvironmentVariable
    {
        get
        {
            return _cmdAddEnvironmentVariable ??= new RelayCommand((o) =>
            {
                ExternalRunner.EnvironmentVariables.Add(new ExternalRunner.ObservableKvp<string, string>("", ""));
            });
        }
    }

    private RelayCommand? _cmdDelEnvironmentVariable;
    public RelayCommand CmdDelEnvironmentVariable
    {
        get
        {
            return _cmdDelEnvironmentVariable ??= new RelayCommand((o) =>
            {
                if (o is ExternalRunner.ObservableKvp<string, string> item
                    && ExternalRunner.EnvironmentVariables.Contains(item)
                    && ((item.Key == "" && item.Value == "")
                        || true == MessageBoxHelper.Confirm(IoC.Get<ILanguageService>().Translate("confirm_to_delete")))
                   )
                {
                    ExternalRunner.EnvironmentVariables.Remove(item);
                }
            });
        }
    }


    private RelayCommand? _cmdCopyJsonAndShare;
    public RelayCommand CmdCopyJsonAndShare
    {
        get
        {
            return _cmdCopyJsonAndShare ??= new RelayCommand((o) =>
            {
                try
                {
                    Clipboard.SetDataObject(
                        $@"
Runner for {ExternalRunner.OwnerProtocolName}

```
{JsonConvert.SerializeObject(ExternalRunner, Formatting.Indented)}
```
"
                        );
                    if (MessageBoxHelper.Confirm($"You runner({ExternalRunner.Name}) is copied to clipboard, do you want to share to Github?", "Share "))
                    {
                        HyperlinkHelper.OpenUriBySystem("https://github.com/1Remote/PRemoteM/wiki/Share-your-favorite-runner");
                    }
                }
                catch
                {
                    // ignored
                }
            });
        }
    }
}