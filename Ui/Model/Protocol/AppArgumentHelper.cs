using System;
using System.Collections.Generic;
using System.Windows.Documents;
using _1RM.Model.Protocol.Base;
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
#if DEBUG
            // TODO check
#endif
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
            return new Tuple<bool, List<AppArgument>>(false, argumentList);
        }
        return null;
    }

    private static Tuple<bool, List<AppArgument>>? GetFilezilla(string path)
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
                    Key = "-password",
                    IsNullable = true,
                    Description = "The password to use for authentication.",
                    Value = ProtocolBaseWithAddressPortUserPwd.MACRO_PASSWORD,
                    AddBlankAfterValue = true,
                    AddBlankAfterKey = true,
                },
                new AppArgument()
                {
                    Type = AppArgumentType.Const,
                    Name = "others",
                    Key = "",
                    IsNullable = true,
                    Value = "-scale=auto",
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