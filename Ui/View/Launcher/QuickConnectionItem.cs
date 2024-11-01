using System;
using _1RM.Utils;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace _1RM.View.Launcher;

public class QuickConnectionItem
{
    public string Protocol { get; set; }= "";
    public string Host { get; set; } = "";
    public string Misc { get; set; } = "";

    private const string separator = "[:`*_^_*`:]";
    public void SetUserPassword(string username, string password, string privateKeyPath)
    {
        Misc = UnSafeStringEncipher.EncryptOnce($"{username}{separator}{password}{separator}{privateKeyPath}{separator}{DateTime.Now.Millisecond}");
    }

    public (string username, string password, string privateKeyPath) GetUserPassword()
    {
        var userPwd = UnSafeStringEncipher.DecryptOrReturnOriginalString(Misc);
        var parts = userPwd.Split(new[] { separator }, StringSplitOptions.None);
        if (parts.Length == 4)
        {
            return (parts[0], parts[1], parts[2]);
        }
        return ("", "", "");
    }
}