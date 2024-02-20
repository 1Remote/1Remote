using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using _1RM.Model.Protocol.Base;

namespace _1RM.Model.Protocol;


public static class AppArgumentHelper
{
    private static LocalApp? GetChrome(string path)
    {
        if (path.Trim().EndsWith("chrome.exe", StringComparison.OrdinalIgnoreCase)
            || path.IndexOf("msedge", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            var argumentList = new List<AppArgument>
            {
                new AppArgument()
                {
                    Type = AppArgumentType.Normal,
                    Name = "Url",
                    Key = "",
                    IsNullable = false,
                    Value = "",
                    Description = "The url you want to access e.g. google.com",
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = true,
                },
            };
            var app = new LocalApp()
            {
                DisplayName = "Chrome",
                RunWithHosting = false,
                ArgumentList = new ObservableCollection<AppArgument>(argumentList),
            };
            return app;
        }
        return null;
    }

    private static LocalApp? GetFreeRdp(string path)
    {
        if (path.IndexOf("freerdp.exe", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            var argumentList = new List<AppArgument>
            {
                new AppArgument()
                {
                    Type = AppArgumentType.Normal,
                    Name = "Window title",
                    Key = "/t:",
                    IsNullable = false,
                    Value = "",
                    Description = "Window title",
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = true,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Const,
                    Name = "Host",
                    Key = "/v:",
                    IsNullable = false,
                    Description = "The host name or IP address to connect to.",
                    Value = ProtocolBaseWithAddressPort.MACRO_HOST_NAME,
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = true,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Const,
                    Name = "Port",
                    Key = "/port:",
                    IsNullable = false,
                    Description = "The port number to connect to.",
                    Value = ProtocolBaseWithAddressPort.MACRO_PORT,
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = true,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Int,
                    Name = "Domain",
                    Key = "/d:",
                    IsNullable = true,
                    Description = "Domain",
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = true,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Const,
                    Name = "User Name",
                    Key = "/u:",
                    IsNullable = true,
                    Description = "The user name to log in as on the remote machine.",
                    Value = ProtocolBaseWithAddressPortUserPwd.MACRO_USERNAME,
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = true,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Const,
                    Name = "Password",
                    Key = "/p:",
                    IsNullable = true,
                    Description = "The password to use for authentication.",
                    Value = ProtocolBaseWithAddressPortUserPwd.MACRO_PASSWORD,
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = true,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Flag,
                    Name = "/admin",
                    Key = "/admin",
                    IsNullable = false,
                    Description = "Admin (or console) session",
                    AddBlankAfterKey = true,
                    AddBlankAfterValue = true,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Flag,
                    Name = "Fullscreen ",
                    Key = "/f",
                    IsNullable = false,
                    Description = "",
                    AddBlankAfterKey = true,
                    AddBlankAfterValue = true,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Flag,
                    Name = "Multi-monitor",
                    Key = "/multimon",
                    IsNullable = false,
                    Description = "",
                    AddBlankAfterKey = true,
                    AddBlankAfterValue = true,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Flag,
                    Name = "Redirect clipboard",
                    Key = "+clipboard",
                    IsNullable = false,
                    Description = "Enable redirect clipboard",
                    Value = "1",
                    AddBlankAfterKey = true,
                    AddBlankAfterValue = true,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Int,
                    Name = "Width",
                    Key = "/w:",
                    IsNullable = true,
                    Description = "Width",
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = true,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Int,
                    Name = "Height",
                    Key = "/h:",
                    IsNullable = true,
                    Description = "Height",
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = true,
                },
            };
            var app = new LocalApp()
            {
                DisplayName = "FreeRdp",
                RunWithHosting = false,
                ArgumentList = new ObservableCollection<AppArgument>(argumentList),
            };
            return app;
        }
        return null;
    }

    private static LocalApp? GetPuttyArgumentList(string path)
    {
        if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == false) return null;
        if (path.IndexOf("putty", StringComparison.OrdinalIgnoreCase) >= 0
            || path.IndexOf("kitty", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            var argumentList = new List<AppArgument>
            {
                new AppArgument()
                {
                    Type = AppArgumentType.Const,
                    Name = "Host",
                    Key = "-ssh",
                    IsNullable = false,
                    Description = "The host name or IP address to connect to.",
                    Value = ProtocolBaseWithAddressPort.MACRO_HOST_NAME,
                    AddBlankAfterKey = true,
                    AddBlankAfterValue = true,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Const,
                    Name = "Port",
                    Key = "-P",
                    IsNullable = false,
                    Description = "The port number to connect to.",
                    Value = ProtocolBaseWithAddressPort.MACRO_PORT,
                    AddBlankAfterKey = true,
                    AddBlankAfterValue = true,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Const,
                    Name = "User Name",
                    Key = "-l",
                    IsNullable = true,
                    Description = "The user name to log in as on the remote machine.",
                    Value = ProtocolBaseWithAddressPortUserPwd.MACRO_USERNAME,
                    AddBlankAfterKey = true,
                    AddBlankAfterValue = true,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Const,
                    Name = "Password",
                    Key = "-pw",
                    IsNullable = true,
                    Description = "The password to use for authentication.",
                    Value = ProtocolBaseWithAddressPortUserPwd.MACRO_PASSWORD,
                    AddBlankAfterKey = true,
                    AddBlankAfterValue = true,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Selection,
                    Name = "SSH Version",
                    Key = "",
                    IsNullable = false,
                    Selections = new Dictionary<string, string>()
                    {
                        {"-1", "V1"},
                        {"-2", "V2"},
                    },
                    Value = "-2",
                    Description = "The SSH protocol version to use.",
                    AddBlankAfterKey = true,
                    AddBlankAfterValue = true,
                },
            };
            // add auto cmd if kitty
            if (path.IndexOf("kitty", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                argumentList.Add(new AppArgument()
                {
                    Type = AppArgumentType.Normal,
                    Name = "Auto Command",
                    Key = "-cmd",
                    IsNullable = true,
                    Description = "Run command after connected.",
                    AddBlankAfterKey = true,
                    AddBlankAfterValue = true,
                });
            }
            var app = new LocalApp()
            {
                DisplayName = "PuTTY",
                RunWithHosting = true,
                ArgumentList = new ObservableCollection<AppArgument>(argumentList),
            };
            return app;
        }
        return null;
    }


    private static LocalApp? GetWindowsTerminalArgumentList(string path)
    {
        if (path.ToLower() == "wt"
            || path.IndexOf("wt.exe", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            var argumentList = new List<AppArgument>
            {
                new AppArgument()
                {
                    Type = AppArgumentType.Const,
                    Name = "misc1",
                    Key = "",
                    IsNullable = true,
                    Value = @$"-w 1 new-tab --title ""{ProtocolBaseWithAddressPort.MACRO_HOST_NAME}"" --suppressApplicationTitle plink",
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = true,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Const,
                    Name = "Host",
                    Key = "-ssh",
                    IsNullable = false,
                    Description = "The host name or IP address to connect to.",
                    Value = ProtocolBaseWithAddressPort.MACRO_HOST_NAME,
                    AddBlankAfterKey = true,
                    AddBlankAfterValue = true,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Const,
                    Name = "Port",
                    Key = "-P",
                    IsNullable = false,
                    Description = "The port number to connect to.",
                    Value = ProtocolBaseWithAddressPort.MACRO_PORT,
                    AddBlankAfterKey = true,
                    AddBlankAfterValue = true,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Const,
                    Name = "misc2",
                    Key = "",
                    IsNullable = true,
                    Value = "-C -X -no-antispoof",
                    AddBlankAfterKey = true,
                    AddBlankAfterValue = true,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Const,
                    Name = "User Name",
                    Key = "-l",
                    IsNullable = true,
                    Description = "The user name to log in as on the remote machine.",
                    Value = ProtocolBaseWithAddressPortUserPwd.MACRO_USERNAME,
                    AddBlankAfterKey = true,
                    AddBlankAfterValue = true,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Const,
                    Name = "Password",
                    Key = "-pw",
                    IsNullable = true,
                    Description = "The password to use for authentication.",
                    Value = ProtocolBaseWithAddressPortUserPwd.MACRO_PASSWORD,
                    AddBlankAfterKey = true,
                    AddBlankAfterValue = true,
                },
            };
            // add auto cmd if kitty
            if (path.IndexOf("kitty", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                argumentList.Add(new AppArgument()
                {
                    Type = AppArgumentType.Normal,
                    Name = "Auto Command",
                    Key = "-cmd",
                    IsNullable = true,
                    Description = "Run command after connected.",
                    AddBlankAfterKey = true,
                    AddBlankAfterValue = true,
                });
            }
            var app = new LocalApp()
            {
                DisplayName = "Windows Terminal",
                RunWithHosting = false,
                ArgumentList = new ObservableCollection<AppArgument>(argumentList),
            };
            return app;
        }
        return null;
    }


    private static LocalApp? GetWinScp(string path)
    {
        if (path.IndexOf("winSCP.exe", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            // ExePath = @"C:\Program Files (x86)\WinSCP\WinSCP.exe",
            // Arguments = @"sftp://%1RM_USERNAME%:%1RM_PASSWORD%@%1RM_HOSTNAME%:%1RM_PORT%",
            // ArgumentsForPrivateKey = @"sftp://%1RM_USERNAME%@%1RM_HOSTNAME%:%1RM_PORT% /privatekey=%1RM_PRIVATE_KEY_PATH%",
            // RunWithHosting = false,
            var argumentList = new List<AppArgument>
            {
                new AppArgument()
                {
                    Type = AppArgumentType.Selection,
                    Name = "Protocol",
                    Key = "",
                    IsNullable = false,
                    Selections = new Dictionary<string, string>()
                    {
                        {"sftp", "sftp"},
                        {"ftp", "ftp"},
                    },
                    Value = "sftp",
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = false,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Const,
                    Name = "User Name",
                    Key = "://",
                    IsNullable = false,
                    Description = "The user name to log in as on the remote machine.",
                    Value = ProtocolBaseWithAddressPortUserPwd.MACRO_USERNAME,
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = false,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Const,
                    Name = "Password",
                    Key = ":",
                    IsNullable = true,
                    Description = "The password to use for authentication.",
                    Value = ProtocolBaseWithAddressPortUserPwd.MACRO_PASSWORD,
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = false,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Const,
                    Name = "Host",
                    Key = "@",
                    IsNullable = false,
                    Description = "The host name or IP address to connect to.",
                    Value = ProtocolBaseWithAddressPort.MACRO_HOST_NAME,
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = false,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Const,
                    Name = "Port",
                    Key = ":",
                    IsNullable = false,
                    Description = "The port number to connect to.",
                    Value = ProtocolBaseWithAddressPort.MACRO_PORT,
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = false,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Const,
                    Name = "Private key",
                    Key = "/privatekey=",
                    IsNullable = true,
                    Description = "OpenSSH key(When the password is empty, you can choose the key.)",
                    Value = ProtocolBaseWithAddressPortUserPwd.MACRO_PRIVATE_KEY_PATH,
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = false,
                },
            };
            var app = new LocalApp()
            {
                DisplayName = "WinSCP",
                RunWithHosting = false,
                ArgumentList = new ObservableCollection<AppArgument>(argumentList),
            };
            return app;
        }
        return null;
    }

    private static LocalApp? GetFilezilla(string path)
    {
        if (path.IndexOf("filezilla.exe", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            //ExternalRunner.Arguments = "sftp://%1RM_USERNAME%:%1RM_PASSWORD%@%1RM_HOSTNAME%";
            //ExternalRunner.Arguments = "ftp://%1RM_USERNAME%:%1RM_PASSWORD%@%1RM_HOSTNAME%";
            // https://wiki.filezilla-project.org/Command-line_arguments_(Client)
            var argumentList = new List<AppArgument>
            {
                new AppArgument()
                {
                    Type = AppArgumentType.Selection,
                    Name = "Protocol",
                    Key = "",
                    IsNullable = false,
                    Selections = new Dictionary<string, string>()
                    {
                        {"sftp", "sftp"},
                        {"ftp", "ftp"},
                    },
                    Value = "ftp",
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = false,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Const,
                    Name = "User Name",
                    Key = "://",
                    IsNullable = false,
                    Description = "The user name to log in as on the remote machine.",
                    Value = ProtocolBaseWithAddressPortUserPwd.MACRO_USERNAME,
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = false,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Const,
                    Name = "Password",
                    Key = ":",
                    IsNullable = true,
                    Description = "The password to use for authentication.",
                    Value = ProtocolBaseWithAddressPortUserPwd.MACRO_PASSWORD,
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = false,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Const,
                    Name = "Host",
                    Key = "@",
                    IsNullable = false,
                    Description = "The host name or IP address to connect to.",
                    Value = ProtocolBaseWithAddressPort.MACRO_HOST_NAME,
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = false,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Const,
                    Name = "Port",
                    Key = ":",
                    IsNullable = false,
                    Description = "The port number to connect to.",
                    Value = ProtocolBaseWithAddressPort.MACRO_PORT,
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = false,
                },
            };
            var app = new LocalApp()
            {
                DisplayName = "filezilla",
                RunWithHosting = false,
                ArgumentList = new ObservableCollection<AppArgument>(argumentList),
            };
            return app;
        }
        return null;
    }

    private static LocalApp? GetUltraVNC(string path)
    {
        if (path.IndexOf("vncviewer.exe", StringComparison.OrdinalIgnoreCase) >= 0
            || path.IndexOf("uvnc", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            // ExePath = @"C:\Program Files (x86)\uvnc\vncviewer.exe",
            // Arguments = @"%1RM_HOSTNAME%:%1RM_PORT% -password %1RM_PASSWORD%",
            // RunWithHosting = false,
            var argumentList = new List<AppArgument>
            {
                new AppArgument()
                {
                    Type = AppArgumentType.Const,
                    Name = "Host",
                    Key = "",
                    IsNullable = false,
                    Description = "The host name or IP address to connect to.",
                    Value = ProtocolBaseWithAddressPort.MACRO_HOST_NAME,
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = false,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Const,
                    Name = "Port",
                    Key = ":",
                    IsNullable = false,
                    Description = "The port number to connect to.",
                    Value = ProtocolBaseWithAddressPort.MACRO_PORT,
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = true,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Const,
                    Name = "Password",
                    Key = "-password",
                    IsNullable = true,
                    Description = "The password to use for authentication.",
                    Value = ProtocolBaseWithAddressPortUserPwd.MACRO_PASSWORD,
                    AddBlankAfterValue = true,
                    AddBlankAfterKey = true,
                },
            };
            var app = new LocalApp()
            {
                DisplayName = "UltraVNC",
                RunWithHosting = false,
                ArgumentList = new ObservableCollection<AppArgument>(argumentList),
            };
            return app;
        }
        return null;
    }

    private static LocalApp? GetTightVNC(string path)
    {
        if (path.IndexOf("tvnviewer.exe", StringComparison.OrdinalIgnoreCase) >= 0
            || path.IndexOf("TightVNC", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            // ExePath = @"C:\Program Files\TightVNC\tvnviewer.exe",
            // Arguments = @"%1RM_HOSTNAME%::%1RM_PORT% -password=%1RM_PASSWORD% -scale=auto",
            // RunWithHosting = true,
            var argumentList = new List<AppArgument>
            {
                new AppArgument()
                {
                    Type = AppArgumentType.Const,
                    Name = "Host",
                    Key = "",
                    IsNullable = false,
                    Description = "The host name or IP address to connect to.",
                    Value = ProtocolBaseWithAddressPort.MACRO_HOST_NAME,
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = false,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Const,
                    Name = "Port",
                    Key = "::",
                    IsNullable = false,
                    Description = "The port number to connect to.",
                    Value = ProtocolBaseWithAddressPort.MACRO_PORT,
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = true,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Const,
                    Name = "Password",
                    Key = "-password=",
                    IsNullable = true,
                    Description = "The password to use for authentication.",
                    Value = ProtocolBaseWithAddressPortUserPwd.MACRO_PASSWORD,
                    AddBlankAfterValue = true,
                    AddBlankAfterKey = false,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Const,
                    Name = "others",
                    Key = "",
                    IsNullable = true,
                    Value = "-scale=auto",
                    AddBlankAfterValue = true,
                    AddBlankAfterKey = false,
                },
            };
            var app = new LocalApp()
            {
                DisplayName = "TightVNC",
                RunWithHosting = true,
                ArgumentList = new ObservableCollection<AppArgument>(argumentList),
            };
            return app;
        }
        return null;
    }

    public static LocalApp? GetPresetArgumentList(string exePath)
    {
        exePath = exePath.ToLower();
        return GetPuttyArgumentList(exePath)
               ?? GetWindowsTerminalArgumentList(exePath)
               ?? GetChrome(exePath)
               ?? GetFreeRdp(exePath)
               ?? GetWinScp(exePath)
               ?? GetFilezilla(exePath)
               ?? GetTightVNC(exePath)
               ?? GetUltraVNC(exePath)
               ?? null;
    }
}