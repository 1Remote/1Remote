using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using Newtonsoft.Json;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.View.Host;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.Utils.KiTTY.Model;
using _1RM.Model.ProtocolRunner.Default;

namespace _1RM.Utils.KiTTY
{
    public interface IKittyConnectable
    {
        /// <summary>
        /// Allowing implementing interface only for specific class 'ProtocolBase'
        /// </summary>
        [JsonIgnore]
        ProtocolBase ProtocolBase { get; }
        string ExternalKittySessionConfigPath { get; set; }
        string GetExeArguments(string sessionName);
    }

    public static class PuttyConnectableExtension
    {
        public static void ConfigKitty(this IKittyConnectable iKittyConnectable, string sessionName, KittyRunner kittyRunner, string sshPrivateKeyPath)
        {
            // install kitty if `kittyRunner.PuttyExePath` not exists
            if (string.IsNullOrEmpty(kittyRunner.PuttyExePath) || File.Exists(kittyRunner.PuttyExePath) == false)
            {
                PuttyConnectableExtension.InstallKitty();
                kittyRunner.PuttyExePath = PuttyConnectableExtension.GetInternalKittyExeFullName();
            }
            WriteKiTTYDefaultConfig(kittyRunner.PuttyExePath);

            // create session config
            var puttyOption = new KittyConfig(sessionName, iKittyConnectable.ExternalKittySessionConfigPath);
            if (iKittyConnectable is SSH server)
            {
                if (!string.IsNullOrEmpty(sshPrivateKeyPath))
                {
                    // set key
                    puttyOption.Set(EnumKittyConfigKey.PublicKeyFile, sshPrivateKeyPath);
                }
                puttyOption.Set(EnumKittyConfigKey.HostName, server.Address);
                puttyOption.Set(EnumKittyConfigKey.PortNumber, server.GetPort());
                puttyOption.Set(EnumKittyConfigKey.Protocol, "ssh");
            }

            // set theme
            var options = PuttyThemes.Themes[kittyRunner.PuttyThemeName];
            foreach (var option in options)
            {
                try
                {
                    if (Enum.TryParse(option.Key, out EnumKittyConfigKey key))
                    {
                        if (option.ValueKind == RegistryValueKind.DWord)
                            puttyOption.Set(key, (int)(option.Value));
                        else
                            puttyOption.Set(key, (string)option.Value);
                    }
                }
                catch (Exception)
                {
                    SimpleLogHelper.Warning($"Putty theme error: can't set up key(value)=> {option.Key}({option.ValueKind})");
                }
            }

            puttyOption.Set(EnumKittyConfigKey.FontHeight, kittyRunner.PuttyFontSize);
            puttyOption.SaveToKittyConfig(kittyRunner.PuttyExePath);
        }

        public static void DelKittySessionConfig(string sessionName, string kittyExePath)
        {
            var fi = new FileInfo(kittyExePath);
            var kittyExeFolderPath = fi!.Directory!.FullName;
            var puttyOption = new KittyConfig(sessionName);
            puttyOption.DelFromKittyConfig(kittyExeFolderPath);
        }


        public static void InstallKitty()
        {
            var kittyDefaultFullName = GetInternalKittyExeFullName();
            var fi = new FileInfo(kittyDefaultFullName);
            if (fi?.Directory?.Exists == false)
                fi.Directory.Create();

            var kitty = System.Windows.Application.GetResourceStream(ResourceUriHelper.GetUriFromCurrentAssembly("Resources/KiTTY/kitty_portable.exe")).Stream;
            if (File.Exists(kittyDefaultFullName))
            {
                // verify MD5
                var md5 = MD5Helper.GetMd5Hash32BitString(File.ReadAllBytes(kittyDefaultFullName));
                byte[] bytes = new byte[kitty.Length];
                kitty.Read(bytes, 0, bytes.Length);
                var md5_2 = MD5Helper.GetMd5Hash32BitString(bytes);
                if (md5_2 != md5)
                {
                    foreach (var process in Process.GetProcessesByName(fi!.Name.ToLower().ReplaceLast(".exe", "")))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch
                        {
                        }
                    }
                    File.Delete(kittyDefaultFullName);
                    using var fileStream = File.Create(kittyDefaultFullName);
                    kitty.Seek(0, SeekOrigin.Begin);
                    kitty.CopyTo(fileStream);
                }
            }
            else
            {
                using (var fileStream = File.Create(kittyDefaultFullName))
                {
                    kitty.Seek(0, SeekOrigin.Begin);
                    kitty.CopyTo(fileStream);
                }
                kitty.Close();
            }

            WriteKiTTYDefaultConfig(kittyDefaultFullName);
        }

        public static void WriteKiTTYDefaultConfig(string kittyFullName)
        {
            var fi = new FileInfo(kittyFullName);
            if (fi?.Directory?.Exists == false)
                fi.Directory.Create();
            File.WriteAllText(Path.Combine(fi!.Directory!.FullName, "kitty.ini"),
                @"
[Agent]
[ConfigBox]
dblclick=open
filter=yes
height=21
[KiTTY]
adb=yes
; antiidle: character string regularly sent to maintain the connection alive
antiidle=
; antiidledelay: time delay between two sending
antiidledelay=60
; autoreconnect: enable/disable the automatic reconnection feature
autoreconnect=yes
backgroundimage=no
capslock=no
conf=yes
ctrltab=no
cygterm=no
hyperlink=yes
icon=no
maxblinkingtime=5
mouseshortcuts=yes
paste=no
ReconnectDelay=5
size=no
transparency=yes
userpasssshnosave=no
winrol=yes
wintitle=yes
zmodem=yes
[Shortcuts]
;input=SHIFT+CONTROL+ALT+F11
;inputm=SHIFT+CONTROL+ALT+F12
;rollup=SHIFT+CONTROL+ALT+F10
[Print]
height=100
maxline=60
maxchar=85
[Launcher]
reload=yes
");
        }

        public static string GetInternalKittyExeFullName()
        {
            string kittyExeName = $"kitty_portable_{Assert.APP_NAME}.exe";
            if (!Directory.Exists(AppPathHelper.Instance.KittyDirPath))
                Directory.CreateDirectory(AppPathHelper.Instance.KittyDirPath);
            var kittyExeFullName = Path.Combine(AppPathHelper.Instance.KittyDirPath, kittyExeName);
            return kittyExeFullName;
        }
    }
}
