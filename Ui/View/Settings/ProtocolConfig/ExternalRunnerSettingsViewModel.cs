using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Animation;
using Newtonsoft.Json;
using _1RM.Model.Protocol;
using _1RM.Model.ProtocolRunner;
using _1RM.Service;
using _1RM.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.Controls;
using Shawn.Utils.Wpf.FileSystem;
using Shawn.Utils.Wpf.PageHost;
using Stylet;

namespace _1RM.View.Settings.ProtocolConfig;

public class ExternalRunnerSettingsViewModel
{
    private readonly ILanguageService _languageService;
    public ExternalRunner ExternalRunner { get; }

    public ExternalRunnerSettingsViewModel(ExternalRunner externalRunner, ILanguageService languageService)
    {
        ExternalRunner = externalRunner;
        _languageService = languageService;

        ExternalRunner.PropertyChanged += (sender, args) =>
        {
            IoC.Get<ProtocolConfigurationService>().Save();
        };
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

                // TODO 命令列表和 private static ProtocolSettings InitProtocol<T, T2> 里的有冲突
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
                            ExternalRunner.Arguments = "sftp://%1RM_USERNAME%:%1RM_PASSWORD%@%1RM_HOSTNAME%:%1RM_PORT%";
                        }
                        if (ExternalRunner.OwnerProtocolName == FTP.ProtocolName)
                        {
                            ExternalRunner.Arguments = "ftp://%1RM_USERNAME%:%1RM_PASSWORD%@%1RM_HOSTNAME%:%1RM_PORT%";
                        }
                        ExternalRunner.RunWithHosting = true;
                    }
                    else if (name == "filezilla.exe".ToLower())
                    {
                        if (ExternalRunner.OwnerProtocolName == SFTP.ProtocolName)
                        {
                            ExternalRunner.Arguments = "sftp://%1RM_USERNAME%:%1RM_PASSWORD%@%1RM_HOSTNAME%";
                        }
                        else if (ExternalRunner.OwnerProtocolName == FTP.ProtocolName)
                        {
                            ExternalRunner.Arguments = "ftp://%1RM_USERNAME%:%1RM_PASSWORD%@%1RM_HOSTNAME%";
                        }
                        ExternalRunner.RunWithHosting = false;
                    }
                    else if (name == "VpxClient.exe".ToLower())
                    {
                        ExternalRunner.Arguments = @"-s %1RM_HOSTNAME% -u %1RM_USERNAME% -p %1RM_PASSWORD%";
                        ExternalRunner.RunWithHosting = true;
                    }
                    else if (name.IndexOf("kitty", StringComparison.Ordinal) >= 0 || name.IndexOf("putty", StringComparison.Ordinal) >= 0)
                    {
                        ExternalRunner.Arguments = @"-ssh %1RM_HOSTNAME% -P %1RM_PORT% -l %1RM_USERNAME% -pw %1RM_PASSWORD% -%SSH_VERSION% -cmd ""%STARTUP_AUTO_COMMAND%""";
                        ExternalRunner.RunWithHosting = true;
                    }
                    else if (name == "tvnviewer.exe".ToLower())
                    {
                        ExternalRunner.Arguments = @"%1RM_HOSTNAME%::%1RM_PORT% -password=%1RM_PASSWORD% -scale=auto";
                        ExternalRunner.RunWithHosting = true;
                    }
                    else if (name == "vncviewer.exe".ToLower() || path.ToLower().IndexOf("uvnc", StringComparison.Ordinal) > 0)
                    {
                        ExternalRunner.Arguments = @"%1RM_HOSTNAME%::%1RM_PORT% -password=%1RM_PASSWORD% -scale=auto";
                        ExternalRunner.RunWithHosting = false;
                    }
                }
                IoC.Get<ProtocolConfigurationService>().Save();
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
                IoC.Get<ProtocolConfigurationService>().Save();
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
                        || true == MessageBoxHelper.Confirm(IoC.Translate("confirm_to_delete"), ownerViewModel: IoC.Get<MainWindowViewModel>()))
                   )
                {
                    ExternalRunner.EnvironmentVariables.Remove(item);
                    IoC.Get<ProtocolConfigurationService>().Save();
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
                    if (MessageBoxHelper.Confirm($"You runner({ExternalRunner.Name}) is copied to clipboard, do you want to share to Github?", "Share", ownerViewModel: IoC.Get<MainWindowViewModel>()))
                    {
                        HyperlinkHelper.OpenUriBySystem("https://github.com/1Remote/1Remote/issues/new?assignees=VShawn&labels=area-config&template=runner_sharing.md&title=");
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