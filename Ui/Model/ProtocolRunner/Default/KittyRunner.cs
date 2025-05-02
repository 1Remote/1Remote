using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using _1RM.Utils.KiTTY;
using _1RM.Utils.KiTTY.Model;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.FileSystem;
using _1RM.Model.Protocol.Base;
using _1RM.Model.Protocol;
using System.Text.RegularExpressions;
using _1RM.Service;
using Microsoft.Win32;
using Shawn.Utils;

namespace _1RM.Model.ProtocolRunner.Default
{
    [Obsolete]
    public class KittyRunner : InternalExeRunner
    {
        public new static string Name = "Internal KiTTY";

        [JsonConstructor]
        [Obsolete]
        public KittyRunner(string ownerProtocolName) : base(ownerProtocolName)
        {
            base._name = Name;
            CodePages = new List<string>
            {
                "UTF-8",
                "ISO-8859-1:1998 (Latin-1, West Europe)",
                "ISO-8859-2:1999 (Latin-2, East Europe)",
                "ISO-8859-3:1999 (Latin-3, South Europe)",
                "ISO-8859-4:1998 (Latin-4, North Europe)",
                "ISO-8859-5:1999 (Latin/Cyrillic)",
                "ISO-8859-6:1999 (Latin/Arabic)",
                "ISO-8859-7:1987 (Latin/Greek)",
                "ISO-8859-8:1999 (Latin/Hebrew)",
                "ISO-8859-9:1999 (Latin-5, Turkish)",
                "ISO-8859-10:1998 (Latin-6, Nordic)",
                "ISO-8859-11:2001 (Latin/Thai)",
                "ISO-8859-13:1998 (Latin-7, Baltic)",
                "ISO-8859-14:1998 (Latin-8, Celtic)",
                "ISO-8859-15:1999 (Latin-9, \"euro\")",
                "ISO-8859-16:2001 (Latin-10, Balkan)",
                "KOI8-U",
                "KOI8-R",
                "HP-ROMAN8",
                "VSCII",
                "DEC-MCS",
                "Win1250 (Central European)",
                "Win1251 (Cyrillic)",
                "Win1252 (Western)",
                "Win1253 (Greek)",
                "Win1254 (Turkish)",
                "Win1255 (Hebrew)",
                "Win1256 (Arabic)",
                "Win1257 (Baltic)",
                "Win1258 (Vietnamese)",
                "CP437",
                "CP620 (Mazovia)",
                "CP819",
                "CP852",
                "CP878",
                "Use font encoding",
            };
        }

        private int _puttyFontSize = 14;
        public int PuttyFontSize
        {
            get => _puttyFontSize;
            set => SetAndNotifyIfChanged(ref _puttyFontSize, value);
        }

        public int GetPuttyFontSize()
        {
            return _puttyFontSize > 0 ? _puttyFontSize : 14;
        }

        private string _puttyThemeName = "";
        public string PuttyThemeName
        {
            get => PuttyThemeNames.Contains(_puttyThemeName) ? _puttyThemeName : PuttyThemeNames.First();
            set => SetAndNotifyIfChanged(ref _puttyThemeName, value);
        }

        [JsonIgnore]
        public ObservableCollection<string> PuttyThemeNames => new ObservableCollection<string>(PuttyThemes.Themes.Keys);

        [JsonIgnore]
        public List<string> CodePages { get; }

        private string _lineCodePage = "UTF-8";
        public string LineCodePage
        {
            get => _lineCodePage;
            set => SetAndNotifyIfChanged(ref _lineCodePage, value);
        }

        public string GetLineCodePageForIni()
        {
            //    { "UTF-8", "UTF-8" },
            //    { "ISO-8859-1:1998 (Latin-1, West Europe)", "ISO-8859-1%3A1998%20(Latin-1,%20West%20Europe)" },
            //    { "ISO-8859-5:1999 (Latin/Cyrillic)", "ISO-8859-5%3A1999%20(Latin%2FCyrillic)" },
            //    { "ISO-8859-15:1999 (Latin-9, \"euro\")", "ISO-8859-15%3A1999%20(Latin-9,%20%22euro%22)" },
            //    { "CP437", "CP437" },
            return _lineCodePage.Trim()
                .Replace(" ", "%20")
                .Replace("\"", "%22")
                .Replace("/", "%2F")
                .Replace(":", "%3A");
        }

        private string _puttyExePath = "";
        public string ExePath
        {
            get => (File.Exists(_puttyExePath) ? _puttyExePath : PuttyConfig.GetInternalPuttyExeFullName()).Replace(Environment.CurrentDirectory, ".");
            set => SetAndNotifyIfChanged(ref _puttyExePath, value.Replace(Environment.CurrentDirectory, "."));
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
                        initPath = new FileInfo(ExePath).DirectoryName;
                    }
                    catch
                    {
                        // ignored
                    }


                    var path = SelectFileHelper.OpenFile(filter: "exe|*.exe", checkFileExists: true, initialDirectory: initPath);
                    if (path == null) return;
                    ExePath = path;
                });
            }
        }

        public override void Install(string path = "")
        {
            if (string.IsNullOrEmpty(path))
                path = GetExeInstallPath();
            _1RM.Utils.KiTTY.Model.Utils.Install("Resources/KiTTY/kitty_portable.exe", path);
            KittyConfig.WriteKittyDefaultConfig(path);
        }

        public override string GetExeInstallPath()
        {
            string kittyExeName = $"kitty_portable_{Assert.APP_NAME}.exe";
            if (!Directory.Exists(AppPathHelper.Instance.KittyDirPath))
                Directory.CreateDirectory(AppPathHelper.Instance.KittyDirPath);
            var kittyExeFullName = Path.Combine(AppPathHelper.Instance.KittyDirPath, kittyExeName);
            return kittyExeFullName;
        }


        private static bool ValidateIPv6(string ipAddress)
        {
            string pattern = @"^([\da-fA-F]{1,4}:){7}[\da-fA-F]{1,4}$";
            Regex regex = new Regex(pattern);

            return regex.IsMatch(ipAddress);
        }

        public override string GetExeArguments(ProtocolBase protocol)
        {
            var p = (protocol.Clone() as ProtocolBase)!;
            if (p is ProtocolBaseWithAddressPortUserPwd)
            {
                p.DecryptToConnectLevel();
            }

            if (p is SSH ssh)
            {
                //var arg = $@" -load ""{ssh.SessionId}"" {ssh.Address} -P {ssh.Port} -l {ssh.UserName} -pw {ssh.Password} -{(int)(ssh.SshVersion ?? 2)} -cmd ""{ssh.StartupAutoCommand}""";
                //var template = $@" -load ""{this.GetSessionId()}"" %1RM_HOSTNAME% -P %1RM_PORT% -l %1RM_USERNAME% -pw %1RM_PASSWORD% -%SSH_VERSION% -cmd ""%STARTUP_AUTO_COMMAND%""";
                //var arg = OtherNameAttributeExtensions.Replace(ssh, template);
                var ipv6 = ValidateIPv6(ssh.Address) ? " -6 " : "";
                var arg = $""" -load "{protocol.SessionId}" {ssh.Address} -P {ssh.Port} -l {ssh.UserName} -pw {ssh.Password} -{(int)(ssh.SshVersion ?? 2)} -cmd "{ssh.StartupAutoCommand}" {ipv6}""";
                return " " + arg;
            }

            if (p is Telnet tel)
            {
                return $""" -load "{protocol.SessionId}" -telnet {tel.Address} -P {tel.Port}""";
            }

            if (p is Serial serial)
            {
                // https://stackoverflow.com/questions/35411927/putty-command-line-automate-serial-commands-from-file
                // https://documentation.help/PuTTY/using-cmdline-sercfg.html
                // Any single digit from 5 to 9 sets the number of data bits.
                // ‘1’, ‘1.5’ or ‘2’ sets the number of stop bits.
                // Any other numeric string is interpreted as a baud rate.
                // A single lower-case letter specifies the parity: ‘n’ for none, ‘o’ for odd, ‘e’ for even, ‘m’ for mark and ‘s’ for space.
                // A single upper-case letter specifies the flow control: ‘N’ for none, ‘X’ for XON/XOFF, ‘R’ for RTS/CTS and ‘D’ for DSR/DTR.
                // For example, ‘-sercfg 19200,8,n,1,N’ denotes a baud rate of 19200, 8 data bits, no parity, 1 stop bit and no flow control.
                serial.DecryptToConnectLevel();
                return $""" -load "{protocol.SessionId}" -serial {serial.SerialPort} -sercfg {serial.BitRate},{serial.DataBits},{serial.GetParityFlag()},{serial.StopBits},{serial.GetFlowControlFlag()}""";
            }
            throw new NotSupportedException($"The protocol type {p.GetType()} is not supported for PuttyRunner.");
        }


        [Obsolete]
        public void ConfigKitty(IKittyConnectable iKittyConnectable, string sessionId, string sshPrivateKeyPath)
        {
            var kittyRunner = this;
            // install kitty if `kittyRunner.PuttyExePath` not exists
            if (string.IsNullOrEmpty(kittyRunner.ExePath) || File.Exists(kittyRunner.ExePath) == false)
            {
                kittyRunner.ExePath = GetExeInstallPath();
                if (File.Exists(kittyRunner.ExePath) == false)
                    Install();
            }
            KittyConfig.WriteKittyDefaultConfig(kittyRunner.ExePath);

            // create session config
            var puttyOption = new KittyConfig(sessionId);
            puttyOption.Set(EnumKittyConfigKey.LineCodePage, kittyRunner.GetLineCodePageForIni());
            puttyOption.ApplyOverwriteSession(iKittyConnectable.ExternalKittySessionConfigPath);

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
            if (iKittyConnectable is Serial serial)
            {
                puttyOption.Set(EnumKittyConfigKey.BackspaceIsDelete, 0);
                puttyOption.Set(EnumKittyConfigKey.LinuxFunctionKeys, 4);

                //SerialLine\COM1\
                //SerialSpeed\9600\
                //SerialDataBits\8\
                //SerialStopHalfbits\2\
                //SerialParity\0\
                //SerialFlowControl\1\
                puttyOption.Set(EnumKittyConfigKey.Protocol, "serial");
                puttyOption.Set(EnumKittyConfigKey.SerialLine, serial.SerialPort);
                puttyOption.Set(EnumKittyConfigKey.SerialSpeed, serial.GetBitRate());
                puttyOption.Set(EnumKittyConfigKey.SerialDataBits, serial.DataBits);
                puttyOption.Set(EnumKittyConfigKey.SerialStopHalfbits, serial.StopBits);
                puttyOption.Set(EnumKittyConfigKey.SerialParity, serial.Parity);
                puttyOption.Set(EnumKittyConfigKey.SerialFlowControl, serial.FlowControl);
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
            puttyOption.SaveToKittyConfig(kittyRunner.ExePath);
        }
    }
}
