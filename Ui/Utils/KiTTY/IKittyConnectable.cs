using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using Newtonsoft.Json;
using PRM.Model;
using PRM.Model.Protocol;
using PRM.Model.Protocol.Base;
using PRM.Service;
using PRM.View.Host;
using Shawn.Utils;
using Shawn.Utils.Wpf;

namespace PRM.Utils.KiTTY
{
    public interface IKittyConnectable
    {
        string GetPuttyConnString(PrmContext context);
        /// <summary>
        /// Allowing implementing interface only for specific class 'ProtocolBase'
        /// </summary>
        [JsonIgnore]
        ProtocolBase ProtocolBase { get; }
        string ExternalKittySessionConfigPath { get; set; }
        string GetExeFullPath();
        string GetExeArguments(PrmContext context);
    }

    public static class PuttyConnectableExtension
    {
        public static string GetSessionName(this IKittyConnectable item)
        {
            if (item is ProtocolBase protocolServer)
            {
                return $"{ConfigurationService.AppName}_{protocolServer.Protocol}_{protocolServer.Id}";
            }
            throw new NotSupportedException("you should not access here! something goes wrong");
        }

        public static void SetKittySessionConfig(this IKittyConnectable iKittyConnectable, int fontSize, string themeName, string sshPrivateKeyPath)
        {
            var kittyExeFullName = GetKittyExeFullName();
            var fi = new FileInfo(kittyExeFullName);
            if (fi.Directory.Exists == false)
                fi.Directory.Create();
            if (fi.Exists == false)
                iKittyConnectable.InstallKitty();
            var kittyExeFolderPath = fi.Directory.FullName;

            var puttyOption = new KittyConfig(iKittyConnectable.GetSessionName(), iKittyConnectable.ExternalKittySessionConfigPath);
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

            var themes = PuttyThemes.GetThemes();
            // set color theme
            if (themes.ContainsKey(themeName) == false)
                themeName = themes.Keys.First();

            var options = themes[themeName];
            if (options != null)
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

            puttyOption.Set(EnumKittyConfigKey.FontHeight, fontSize);

            //_puttyOption.Set(PuttyRegOptionKey.Colour0, "255,255,255");
            //_puttyOption.Set(PuttyRegOptionKey.Colour1, "255,255,255");
            //_puttyOption.Set(PuttyRegOptionKey.Colour2, "51,51,51");
            //_puttyOption.Set(PuttyRegOptionKey.Colour3, "85,85,85");
            //_puttyOption.Set(PuttyRegOptionKey.Colour4, "0,0,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour5, "0,255,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour6, "77,77,77");
            //_puttyOption.Set(PuttyRegOptionKey.Colour7, "85,85,85");
            //_puttyOption.Set(PuttyRegOptionKey.Colour8, "187,0,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour9, "255,85,85");
            //_puttyOption.Set(PuttyRegOptionKey.Colour10, "152,251,152");
            //_puttyOption.Set(PuttyRegOptionKey.Colour11, "85,255,85");
            //_puttyOption.Set(PuttyRegOptionKey.Colour12, "240,230,140");
            //_puttyOption.Set(PuttyRegOptionKey.Colour13, "255,255,85");
            //_puttyOption.Set(PuttyRegOptionKey.Colour14, "205,133,63");
            //_puttyOption.Set(PuttyRegOptionKey.Colour15, "135,206,235");
            //_puttyOption.Set(PuttyRegOptionKey.Colour16, "255,222,173");
            //_puttyOption.Set(PuttyRegOptionKey.Colour17, "255,85,255");
            //_puttyOption.Set(PuttyRegOptionKey.Colour18, "255,160,160");
            //_puttyOption.Set(PuttyRegOptionKey.Colour19, "255,215,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour20, "245,222,179");
            //_puttyOption.Set(PuttyRegOptionKey.Colour21, "255,255,255");

            //_puttyOption.Set(PuttyRegOptionKey.Colour0, "192,192,192");
            //_puttyOption.Set(PuttyRegOptionKey.Colour1, "255,255,255");
            //_puttyOption.Set(PuttyRegOptionKey.Colour2, "0,0,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour3, "85,85,85");
            //_puttyOption.Set(PuttyRegOptionKey.Colour4, "0,0,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour5, "0,255,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour6, "0,0,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour7, "85,85,85");
            //_puttyOption.Set(PuttyRegOptionKey.Colour8, "255,0,255");
            //_puttyOption.Set(PuttyRegOptionKey.Colour9, "255,85,85");
            //_puttyOption.Set(PuttyRegOptionKey.Colour10,"0,255,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour11,"85,255,85");
            //_puttyOption.Set(PuttyRegOptionKey.Colour12,"187,187,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour13,"255,255,85");
            //_puttyOption.Set(PuttyRegOptionKey.Colour14,"0,255,255");
            //_puttyOption.Set(PuttyRegOptionKey.Colour15,"0,0,255");
            //_puttyOption.Set(PuttyRegOptionKey.Colour16,"0,0,255");
            //_puttyOption.Set(PuttyRegOptionKey.Colour17,"255,85,255");
            //_puttyOption.Set(PuttyRegOptionKey.Colour18,"0,187,187");
            //_puttyOption.Set(PuttyRegOptionKey.Colour19,"85,255,255");
            //_puttyOption.Set(PuttyRegOptionKey.Colour20,"187,187,187");
            //_puttyOption.Set(PuttyRegOptionKey.Colour21,"255,255,255");

            //_puttyOption.Set(PuttyRegOptionKey.UseSystemColours, 0);
            //_puttyOption.Set(PuttyRegOptionKey.TryPalette, 0);
            //_puttyOption.Set(PuttyRegOptionKey.ANSIColour, 1);
            //_puttyOption.Set(PuttyRegOptionKey.Xterm256Colour, 1);
            //_puttyOption.Set(PuttyRegOptionKey.BoldAsColour, 1);

            //_puttyOption.Set(PuttyRegOptionKey.Colour0, "211,215,207");
            //_puttyOption.Set(PuttyRegOptionKey.Colour1, "238,238,236");
            //_puttyOption.Set(PuttyRegOptionKey.Colour2, "46,52,54");
            //_puttyOption.Set(PuttyRegOptionKey.Colour3, "85,87,83");
            //_puttyOption.Set(PuttyRegOptionKey.Colour4, "0,0,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour5, "0,255,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour6, "46,52,54");
            //_puttyOption.Set(PuttyRegOptionKey.Colour7, "85,87,83");
            //_puttyOption.Set(PuttyRegOptionKey.Colour8, "204,0,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour9, "239,41,41");
            //_puttyOption.Set(PuttyRegOptionKey.Colour10,"78,154,6");
            //_puttyOption.Set(PuttyRegOptionKey.Colour11,"138,226,52");
            //_puttyOption.Set(PuttyRegOptionKey.Colour12,"196,160,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour13,"252,233,79");
            //_puttyOption.Set(PuttyRegOptionKey.Colour14,"52,101,164");
            //_puttyOption.Set(PuttyRegOptionKey.Colour15,"114,159,207");
            //_puttyOption.Set(PuttyRegOptionKey.Colour16,"117,80,123");
            //_puttyOption.Set(PuttyRegOptionKey.Colour17,"173,127,168");
            //_puttyOption.Set(PuttyRegOptionKey.Colour18,"6,152,154");
            //_puttyOption.Set(PuttyRegOptionKey.Colour19,"52,226,226");
            //_puttyOption.Set(PuttyRegOptionKey.Colour20,"211,215,207");
            //_puttyOption.Set(PuttyRegOptionKey.Colour21,"238,238,236");

            puttyOption.SaveToKittyConfig(kittyExeFolderPath);
        }

        public static void DelKittySessionConfig(this IKittyConnectable iKittyConnectable)
        {
            var kittyExeFullName = GetKittyExeFullName();
            var fi = new FileInfo(kittyExeFullName);
            if (fi.Directory.Exists == false)
                fi.Directory.Create();
            if (fi.Exists == false)
                iKittyConnectable.InstallKitty();
            var kittyExeFolderPath = fi.Directory.FullName;

            var puttyOption = new KittyConfig(iKittyConnectable.GetSessionName());
            puttyOption.DelFromKittyConfig(kittyExeFolderPath);
        }

        public static void InstallKitty(this IKittyConnectable iKittyConnectable)
        {
            var kittyExeFullName = GetKittyExeFullName();
            var fi = new FileInfo(kittyExeFullName);
            if (fi.Directory.Exists == false)
                fi.Directory.Create();

            var kitty = System.Windows.Application.GetResourceStream(ResourceUriHelper.GetUriFromCurrentAssembly("Resources/KiTTY/kitty_portable.exe")).Stream;
            if (File.Exists(kittyExeFullName))
            {
                // verify MD5
                var md5 = MD5Helper.GetMd5Hash32BitString(File.ReadAllBytes(kittyExeFullName));
                byte[] bytes = new byte[kitty.Length];
                kitty.Read(bytes, 0, bytes.Length);
                var md5_2 = MD5Helper.GetMd5Hash32BitString(bytes);
                if (md5_2 != md5)
                {
                    foreach (var process in Process.GetProcessesByName(fi.Name.ToLower().ReplaceLast(".exe", "")))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch
                        {
                        }
                    }
                    File.Delete(kittyExeFullName);
                    using var fileStream = File.Create(kittyExeFullName);
                    kitty.Seek(0, SeekOrigin.Begin);
                    kitty.CopyTo(fileStream);
                }
            }
            else
            {
                using (var fileStream = File.Create(kittyExeFullName))
                {
                    kitty.Seek(0, SeekOrigin.Begin);
                    kitty.CopyTo(fileStream);
                }
                kitty.Close();
            }

            File.WriteAllText(Path.Combine(fi.Directory.FullName, "kitty.ini"),
                @"
[Agent]
[ConfigBox]
dblclick=open
filter=yes
height=21
[KiTTY]
adb=yes
; antiidle: character string regularly sent to maintain the connection alive
antiidle=\k08\
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

        public static string GetKittyExeFullName()
        {
#if DEV
            const string kittyExeName = "kitty_portable_PRemoteM_debug.exe";
#else
            const string kittyExeName = "kitty_portable_PRemoteM.exe";
#endif
#if FOR_MICROSOFT_STORE_ONLY
            var kittyExeFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ConfigurationService.AppName, "Kitty");
#else
            var kittyExeFolderPath = Path.Combine(Environment.CurrentDirectory, "Kitty");
            try
            {
                var kittyExeFolderPathAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ConfigurationService.AppName, "Kitty");
                if (Directory.Exists(kittyExeFolderPathAppData))
                    Directory.Delete(kittyExeFolderPathAppData, true);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
#endif

            if (!Directory.Exists(kittyExeFolderPath))
                Directory.CreateDirectory(kittyExeFolderPath);
            var kittyExeFullName = Path.Combine(kittyExeFolderPath, kittyExeName);
            return kittyExeFullName;
        }

        public static string GetKittyExeFullName(this IKittyConnectable iKittyConnectable)
        {
            return GetKittyExeFullName();
        }
    }
}
