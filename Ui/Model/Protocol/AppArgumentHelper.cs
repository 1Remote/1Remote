using System;
using System.Collections.Generic;
using System.Windows.Documents;
using _1RM.Utils.KiTTY;

namespace _1RM.Model.Protocol;


public static class AppArgumentHelper
{
    private static Tuple<bool, List<AppArgument>>? GetFreeRdp(string path)
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
                    Value = "%TITLE%",
                    Description = "Window title",
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = true,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Normal,
                    Name = "Host",
                    Key = "/v:",
                    IsNullable = false,
                    Description = "The host name or IP address to connect to.",
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = true,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Int,
                    Name = "Port",
                    Key = "/port:",
                    IsNullable = false,
                    Description = "The port number to connect to.",
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = true,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Int,
                    Name = "Domain",
                    Key = "/d:",
                    IsNullable = false,
                    Description = "Domain",
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = true,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Normal,
                    Name = "User Name",
                    Key = "/u:",
                    IsNullable = false,
                    Description = "The user name to log in as on the remote machine.",
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = true,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Secret,
                    Name = "Password",
                    Key = "/p:",
                    IsNullable = false,
                    Description = "The password to use for authentication.",
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
            // TODO add auto cmd if kitty
            return new Tuple<bool, List<AppArgument>>(true, argumentList);
        }
        return null;
    }

    private static Tuple<bool, List<AppArgument>>? GetPuttyArgumentList(string path)
    {
        if (path.IndexOf("putty.exe", StringComparison.OrdinalIgnoreCase) >= 0
            || path.IndexOf("kitty.exe", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            var argumentList = new List<AppArgument>
            {
                new AppArgument()
                {
                    Type = AppArgumentType.Normal,
                    Name = "Host",
                    Key = "-ssh",
                    IsNullable = false,
                    Description = "The host name or IP address to connect to.",
                    AddBlankAfterKey = true,
                    AddBlankAfterValue = true,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Int,
                    Name = "Port",
                    Key = "-P",
                    IsNullable = false,
                    Description = "The port number to connect to.",
                    AddBlankAfterKey = true,
                    AddBlankAfterValue = true,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Normal,
                    Name = "User Name",
                    Key = "-l",
                    IsNullable = false,
                    Description = "The user name to log in as on the remote machine.",
                    AddBlankAfterKey = true,
                    AddBlankAfterValue = true,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Secret,
                    Name = "Password",
                    Key = "-pw",
                    IsNullable = false,
                    Description = "The password to use for authentication.",
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
                    Description = "The SSH protocol version to use.",
                    AddBlankAfterKey = true,
                    AddBlankAfterValue = true,
                },
            };
            // TODO add auto cmd if kitty
            return new Tuple<bool, List<AppArgument>>(true, argumentList);
        }
        return null;
    }

    private static Tuple<bool, List<AppArgument>>? GetWinScp(string path)
    {
        if (path.IndexOf("WinSCP", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            // ExePath = @"C:\Program Files (x86)\WinSCP\WinSCP.exe",
            // Arguments = @"sftp://%USERNAME%:%PASSWORD%@%HOSTNAME%:%PORT%",
            // ArgumentsForPrivateKey = @"sftp://%USERNAME%@%HOSTNAME%:%PORT% /privatekey=%SSH_PRIVATE_KEY_PATH%",
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
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = false,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Normal,
                    Name = "User Name",
                    Key = "://",
                    IsNullable = false,
                    Description = "The user name to log in as on the remote machine.",
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = false,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Secret,
                    Name = "Password",
                    Key = ":",
                    IsNullable = true,
                    Description = "The password to use for authentication.",
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = false,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Normal,
                    Name = "Host",
                    Key = "@",
                    IsNullable = false,
                    Description = "The host name or IP address to connect to.",
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = false,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Int,
                    Name = "Port",
                    Key = ":",
                    IsNullable = false,
                    Description = "The port number to connect to.",
                    AddBlankAfterKey = true,
                    AddBlankAfterValue = false,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.File,
                    Name = "Private key",
                    Key = "/privatekey=",
                    IsNullable = true,
                    Description = "OpenSSH key(When the password is empty, you can choose the key.)",
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = false,
                },
            };
            return new Tuple<bool, List<AppArgument>>(false, argumentList);
        }
        return null;
    }

    private static Tuple<bool, List<AppArgument>>? GetFilezilla(string path)
    {
        if (path.IndexOf("filezilla.exe", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            //ExternalRunner.Arguments = "sftp://%USERNAME%:%PASSWORD%@%HOSTNAME%";
            //ExternalRunner.Arguments = "ftp://%USERNAME%:%PASSWORD%@%HOSTNAME%";
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
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = false,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Normal,
                    Name = "User Name",
                    Key = "://",
                    IsNullable = false,
                    Description = "The user name to log in as on the remote machine.",
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = false,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Secret,
                    Name = "Password",
                    Key = ":",
                    IsNullable = true,
                    Description = "The password to use for authentication.",
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = false,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Normal,
                    Name = "Host",
                    Key = "@",
                    IsNullable = false,
                    Description = "The host name or IP address to connect to.",
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = false,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Int,
                    Name = "Port",
                    Key = ":",
                    IsNullable = false,
                    Description = "The port number to connect to.",
                    AddBlankAfterKey = true,
                    AddBlankAfterValue = false,
                },
            };
            return new Tuple<bool, List<AppArgument>>(false, argumentList);
        }
        return null;
    }

    private static Tuple<bool, List<AppArgument>>? GetUltraVNC(string path)
    {
        if (path.IndexOf("vncviewer.exe", StringComparison.OrdinalIgnoreCase) >= 0
            || path.IndexOf("uvnc", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            // ExePath = @"C:\Program Files (x86)\uvnc\vncviewer.exe",
            // Arguments = @"%HOSTNAME%:%PORT% -password %PASSWORD%",
            // RunWithHosting = false,
            var argumentList = new List<AppArgument>
            {
                new AppArgument()
                {
                    Type = AppArgumentType.Normal,
                    Name = "Host",
                    Key = "",
                    IsNullable = false,
                    Description = "The host name or IP address to connect to.",
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = false,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Int,
                    Name = "Port",
                    Key = ":",
                    IsNullable = false,
                    Description = "The port number to connect to.",
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = true,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Secret,
                    Name = "Password",
                    Key = "-password",
                    IsNullable = true,
                    Description = "The password to use for authentication.",
                    AddBlankAfterValue = true,
                    AddBlankAfterKey = true,
                },
            };
            return new Tuple<bool, List<AppArgument>>(false, argumentList);
        }
        return null;
    }

    private static Tuple<bool, List<AppArgument>>? GetTightVNC(string path)
    {
        if (path.IndexOf("tvnviewer.exe", StringComparison.OrdinalIgnoreCase) >= 0
            || path.IndexOf("TightVNC", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            // ExePath = @"C:\Program Files\TightVNC\tvnviewer.exe",
            // Arguments = @"%HOSTNAME%::%PORT% -password=%PASSWORD% -scale=auto",
            // RunWithHosting = true,
            var argumentList = new List<AppArgument>
            {
                new AppArgument()
                {
                    Type = AppArgumentType.Normal,
                    Name = "Host",
                    Key = "",
                    IsNullable = false,
                    Description = "The host name or IP address to connect to.",
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = false,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Int,
                    Name = "Port",
                    Key = "::",
                    IsNullable = false,
                    Description = "The port number to connect to.",
                    AddBlankAfterKey = false,
                    AddBlankAfterValue = true,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Secret,
                    Name = "Password",
                    Key = "-scale=auto -password",
                    IsNullable = true,
                    Description = "The password to use for authentication.",
                    AddBlankAfterValue = true,
                    AddBlankAfterKey = true,
                },
            };
            return new Tuple<bool, List<AppArgument>>(true, argumentList);
        }
        return null;
    }

    public static Tuple<bool, List<AppArgument>>? GetPresetArgumentList(string exePath)
    {
        exePath = exePath.ToLower();
        return GetPuttyArgumentList(exePath)
               ?? GetFreeRdp(exePath)
               ?? GetWinScp(exePath)
               ?? GetFilezilla(exePath)
               ?? GetTightVNC(exePath)
               ?? GetUltraVNC(exePath)
               ?? null;
    }
}