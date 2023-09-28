using System.Collections.Generic;
using System.Windows.Documents;

namespace _1RM.Model.Protocol;


public static class AppArgumentHelper
{
    private static List<AppArgument> GetPuttyArgumentList()
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
                AddBlankAfterValue = true,
                AddBlankAfterKey = true,
            },
            new AppArgument()
            {
                Type = AppArgumentType.Int,
                Name = "Port",
                Key = "-P",
                IsNullable = false,
                Description = "The port number to connect to.",
                AddBlankAfterValue = true,
                AddBlankAfterKey = true,
            },
            new AppArgument()
            {
                Type = AppArgumentType.Normal,
                Name = "User Name",
                Key = "-l",
                IsNullable = false,
                Description = "The user name to log in as on the remote machine.",
                AddBlankAfterValue = true,
                AddBlankAfterKey = true,
            },
            new AppArgument()
            {
                Type = AppArgumentType.Secret,
                Name = "Password",
                Key = "-pw",
                IsNullable = false,
                Description = "The password to use for authentication.",
                AddBlankAfterValue = true,
                AddBlankAfterKey = true,
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
                AddBlankAfterValue = true,
                AddBlankAfterKey = true,
            },
        };
        return argumentList;
    }

    private static List<AppArgument> GetWinScp()
    {
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
                AddBlankAfterValue = false,
                AddBlankAfterKey = false,
            },
            new AppArgument()
            {
                Type = AppArgumentType.Normal,
                Name = "User Name",
                Key = "://",
                IsNullable = false,
                Description = "The user name to log in as on the remote machine.",
                AddBlankAfterValue = false,
                AddBlankAfterKey = false,
            },
            new AppArgument()
            {
                Type = AppArgumentType.Secret,
                Name = "Password",
                Key = ":",
                IsNullable = false,
                Description = "The password to use for authentication.",
                AddBlankAfterValue = false,
                AddBlankAfterKey = false,
            },
            new AppArgument()
            {
                Type = AppArgumentType.Normal,
                Name = "Host",
                Key = "@",
                IsNullable = false,
                Description = "The host name or IP address to connect to.",
                AddBlankAfterValue = false,
                AddBlankAfterKey = false,
            },
            new AppArgument()
            {
                Type = AppArgumentType.Int,
                Name = "Port",
                Key = ":",
                IsNullable = false,
                Description = "The port number to connect to.",
                AddBlankAfterValue = false,
                AddBlankAfterKey = false,
            },
        };
        return argumentList;
    }

    public static List<AppArgument> GetArgumentList(string exePath)
    {
        exePath = exePath.ToLower();
        if (exePath.IndexOf("kitty") > 0 || exePath.IndexOf("putty") > 0)
        {
            return GetPuttyArgumentList();
        }
        if (exePath.IndexOf("winscp") > 0)
        {
            return GetWinScp();
        }
        return new List<AppArgument>();
    }
}