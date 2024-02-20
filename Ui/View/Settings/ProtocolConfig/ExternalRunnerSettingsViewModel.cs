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
using System.Windows.Shapes;

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
            if (args.PropertyName == nameof(Model.ProtocolRunner.ExternalRunner.ExePath))
            {
                AutoArguments();
            }
            IoC.Get<ProtocolConfigurationService>().Save();
        };
    }

    private void AutoArguments()
    {
        if (string.IsNullOrEmpty(ExternalRunner.Arguments))
        {
            var path = ExternalRunner.ExePath;
            var name = new FileInfo(path).Name.ToLower();
            if (name.IndexOf("winscp", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (ExternalRunner.OwnerProtocolName == FTP.ProtocolName)
                {
                    ExternalRunner.Arguments = "ftp://%1RM_USERNAME%:%1RM_PASSWORD%@%1RM_HOSTNAME%:%1RM_PORT%";
                }
                else if (ExternalRunner.OwnerProtocolName == SFTP.ProtocolName)
                {
                    ExternalRunner.Arguments = "sftp://%1RM_USERNAME%:%1RM_PASSWORD%@%1RM_HOSTNAME%:%1RM_PORT%";
                    if (ExternalRunner is ExternalRunnerForSSH ers)
                        ers.ArgumentsForPrivateKey = @"sftp://%1RM_USERNAME%@%1RM_HOSTNAME%:%1RM_PORT% /privatekey=%1RM_PRIVATE_KEY_PATH%";
                }
                ExternalRunner.RunWithHosting = true;
            }


            if (string.IsNullOrEmpty(ExternalRunner.Arguments)
                && name.IndexOf("filezilla", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (ExternalRunner.OwnerProtocolName == FTP.ProtocolName)
                {
                    ExternalRunner.Arguments = "ftp://%1RM_USERNAME%:%1RM_PASSWORD%@%1RM_HOSTNAME%";
                }
                else if (ExternalRunner.OwnerProtocolName == SFTP.ProtocolName)
                {
                    ExternalRunner.Arguments = "sftp://%1RM_USERNAME%:%1RM_PASSWORD%@%1RM_HOSTNAME%";
                }
                ExternalRunner.RunWithHosting = false;
            }


            if (string.IsNullOrEmpty(ExternalRunner.Arguments)
                && ExternalRunner.OwnerProtocolName == SSH.ProtocolName
                && (name.IndexOf("kitty", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("putty", StringComparison.OrdinalIgnoreCase) >= 0
                ))
            {
                ExternalRunner.Arguments = @"-ssh %1RM_HOSTNAME% -P %1RM_PORT% -l %1RM_USERNAME% -pw %1RM_PASSWORD% -%SSH_VERSION% -cmd ""%STARTUP_AUTO_COMMAND%""";
                if (ExternalRunner is ExternalRunnerForSSH ers)
                {
                    // NOT SUPPORTED
                    ers.ArgumentsForPrivateKey = @"";
                }
                ExternalRunner.RunWithHosting = true;
            }


            if (string.IsNullOrEmpty(ExternalRunner.Arguments)
                && (name.IndexOf("wt.exe", StringComparison.OrdinalIgnoreCase) >= 0
                    || name == "wt"
                ))
            {
                if (ExternalRunner.OwnerProtocolName == SSH.ProtocolName)
                {
                    ExternalRunner.Arguments = @"-w 1 new-tab --title ""%1RM_HOSTNAME%"" --suppressApplicationTitle plink -ssh %1RM_HOSTNAME% -P %1RM_PORT% -%SSH_VERSION% -C -X -no-antispoof -l %1RM_USERNAME% -pw %1RM_PASSWORD%";
                    if (ExternalRunner is ExternalRunnerForSSH ers && string.IsNullOrEmpty(ers.ArgumentsForPrivateKey))
                    {
                        ers.ArgumentsForPrivateKey = @"-w 1 new-tab --title ""%1RM_HOSTNAME%"" --suppressApplicationTitle plink -ssh %1RM_HOSTNAME% -P %1RM_PORT% -%SSH_VERSION% -C -X -no-antispoof -l %1RM_USERNAME% -i %1RM_PRIVATE_KEY_PATH%";
                    }
                }
                ExternalRunner.RunWithHosting = false;
            }




            if (string.IsNullOrEmpty(ExternalRunner.Arguments)
                && ExternalRunner.OwnerProtocolName == VNC.ProtocolName
                && name.IndexOf("VpxClient", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                ExternalRunner.Arguments = @"-s %1RM_HOSTNAME% -u %1RM_USERNAME% -p %1RM_PASSWORD%";
                ExternalRunner.RunWithHosting = true;
            }


            if (string.IsNullOrEmpty(ExternalRunner.Arguments)
                && ExternalRunner.OwnerProtocolName == VNC.ProtocolName
                && name.IndexOf("tvnviewer", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                ExternalRunner.Arguments = @"%1RM_HOSTNAME%::%1RM_PORT% -password=%1RM_PASSWORD% -scale=auto";
                ExternalRunner.RunWithHosting = true;
            }



            if (string.IsNullOrEmpty(ExternalRunner.Arguments)
                && ExternalRunner.OwnerProtocolName == VNC.ProtocolName
                && (name.IndexOf("vncviewer", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("uvnc", StringComparison.OrdinalIgnoreCase) >= 0
                ))
            {
                ExternalRunner.Arguments = @"%1RM_HOSTNAME%:%1RM_PORT% -password=%1RM_PASSWORD%";
                ExternalRunner.RunWithHosting = false;
            }
        }
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