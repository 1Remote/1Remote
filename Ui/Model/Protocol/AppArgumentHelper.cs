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
            },
            new AppArgument()
            {
                Type = AppArgumentType.Int,
                Name = "Port",
                Key = "-P",
                IsNullable = false,
                Description = "The port number to connect to.",
            },
            new AppArgument()
            {
                Type = AppArgumentType.Normal,
                Name = "User Name",
                Key = "-l",
                IsNullable = false,
                Description = "The user name to log in as on the remote machine.",
            },
            new AppArgument()
            {
                Type = AppArgumentType.Secret,
                Name = "Password",
                Key = "-pw",
                IsNullable = false,
                Description = "The password to use for authentication.",
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
        return new List<AppArgument>();
    }
}