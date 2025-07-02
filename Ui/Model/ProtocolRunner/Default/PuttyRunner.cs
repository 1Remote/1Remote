using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using Newtonsoft.Json;
using Microsoft.Win32;
using _1RM.Utils.PuTTY;
using _1RM.Utils.PuTTY.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.FileSystem;
using System.Windows;
using static System.Windows.Forms.Design.AxImporter;

namespace _1RM.Model.ProtocolRunner.Default
{
    public class PuttyRunner : InternalExeRunner
    {
        public new static string Name = "Built-in PuTTY";

        [JsonConstructor]
        public PuttyRunner(string ownerProtocolName) : base(ownerProtocolName)
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

            LoadColours();
        }

        /// <summary>
        /// FOR puttyOption.Set
        /// </summary>
        [JsonIgnore] public string PuttyFontSource { get; set; } = "Consolas";


        private string _puttyFont = "Consolas";
        /// <summary>
        /// FOR UI BINDING
        /// </summary>
        public string PuttyFont
        {
            get => _puttyFont;
            set
            {
                SetAndNotifyIfChanged(ref _puttyFont, value);
                PuttyFontSource =
                    Fonts.SystemFontFamilies.FirstOrDefault(x => x.FamilyNames.Last().Value == value)?.Source ??
                    "Consolas";
            }
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
            set
            {
                if (SetAndNotifyIfChanged(ref _puttyThemeName, value))
                {
                    LoadColours();
                }
            }
        }

        private void LoadColours()
        {
            if (PuttyThemes.Themes.ContainsKey(PuttyThemeName))
            {
                var options = PuttyThemes.Themes[PuttyThemeName];
                SetBrush(options, nameof(Colour0));
                //SetBrush(options, nameof(Colour1));
                SetBrush(options, nameof(Colour2));
                //SetBrush(options, nameof(Colour3));
                //SetBrush(options, nameof(Colour4));
                //SetBrush(options, nameof(Colour5));
                //SetBrush(options, nameof(Colour6));
                //SetBrush(options, nameof(Colour7));
                //SetBrush(options, nameof(Colour8));
                SetBrush(options, nameof(Colour9));
                SetBrush(options, nameof(Colour10));
                SetBrush(options, nameof(Colour11));
                SetBrush(options, nameof(Colour15));
            }
        }

        [JsonIgnore] public SolidColorBrush Colour0 { get; set; } = Brushes.White;
        //[JsonIgnore] public SolidColorBrush Colour1 { get; set; } = Brushes.Black;
        [JsonIgnore] public SolidColorBrush Colour2 { get; set; } = Brushes.Black;
        //[JsonIgnore] public SolidColorBrush Colour3 { get; set; } = Brushes.Black;
        //[JsonIgnore] public SolidColorBrush Colour4 { get; set; } = Brushes.Black;
        //[JsonIgnore] public SolidColorBrush Colour5 { get; set; } = Brushes.Black;
        //[JsonIgnore] public SolidColorBrush Colour6 { get; set; } = Brushes.Black;
        //[JsonIgnore] public SolidColorBrush Colour7 { get; set; } = Brushes.Black;
        //[JsonIgnore] public SolidColorBrush Colour8 { get; set; } = Brushes.Black;
        [JsonIgnore] public SolidColorBrush Colour9 { get; set; } = Brushes.Black;
        [JsonIgnore] public SolidColorBrush Colour10 { get; set; } = Brushes.Black;
        [JsonIgnore] public SolidColorBrush Colour11 { get; set; } = Brushes.Black;
        [JsonIgnore] public SolidColorBrush Colour15 { get; set; } = Brushes.Black;

        private void SetBrush(List<PuttyConfigKeyValuePair> options, string name)
        {
            var t = this.GetType();
            var prop = t.GetProperty(name);
            if (prop == null) return;

            var option = options.FirstOrDefault(x => string.Equals(x.Key, name, StringComparison.CurrentCultureIgnoreCase));
            if (option != null)
            {
                var rgb = ((string)option.Value).Split(',');
                try
                {
                    if (rgb.Length == 3)
                    {
                        var r = byte.Parse(rgb[0]);
                        var g = byte.Parse(rgb[1]);
                        var b = byte.Parse(rgb[2]);

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
                            prop.SetValue(this, brush, null);
                            //brush = Brushes.LightSeaGreen;
                            RaisePropertyChanged(name);
                        });


                        //// 创建一个 1x1 像素的图像，并填充为指定的颜色
                        //var color = Color.FromRgb(r, g, b);
                        //var bitmap = new WriteableBitmap(1, 1, 96, 96, PixelFormats.Bgra32, null);
                        //bitmap.Lock();
                        //unsafe
                        //{
                        //    IntPtr pBackBuffer = bitmap.BackBuffer;
                        //    *((uint*)pBackBuffer) = (uint)(color.A << 24 | color.R << 16 | color.G << 8 | color.B);
                        //}
                        //bitmap.AddDirtyRect(new Int32Rect(0, 0, 1, 1));
                        //bitmap.Unlock();

                        //// 将生成的图像设置为 ImageBrush
                        //brush = new ImageBrush(bitmap);

                    }
                }
                catch (Exception)
                {
                    // ignored
                }
            }
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



        private static bool ValidateIPv6(string ipAddress)
        {
            string pattern = @"^([\da-fA-F]{1,4}:){7}[\da-fA-F]{1,4}$";
            Regex regex = new Regex(pattern);

            return regex.IsMatch(ipAddress);
        }

        public override string GetExeArguments(ProtocolBase protocol)
        {
            var p = protocol.Clone();
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
                //var arg = $@" -load ""{sessionId}"" {ssh.Address} -P {ssh.Port} -l {ssh.UserName} -pw {ssh.Password} -{(int)(ssh.SshVersion ?? 2)} -cmd ""{ssh.StartupAutoCommand}"" {ipv6}";
                string m = GetAutoCommandFilePath(protocol);
                if (!string.IsNullOrEmpty(m))
                {
                    m = $" -m \"{m}\" -t";
                }
                var arg = $@" -load ""{protocol.SessionId}"" {ssh.Address} -P {ssh.Port} -l {ssh.UserName} -pw {ssh.Password} -{(int)(ssh.SshVersion ?? 2)} {ipv6} {m}";
                return " " + arg;
            }

            if (p is Telnet tel)
            {
                return $@" -load ""{protocol.SessionId}"" -telnet {tel.Address} -P {tel.Port}";
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
                return $@" -load ""{protocol.SessionId}"" -serial {serial.SerialPort} -sercfg {serial.BitRate},{serial.DataBits},{serial.GetParityFlag()},{serial.StopBits},{serial.GetFlowControlFlag()}";
            }
            throw new NotSupportedException($"The protocol type {p.GetType()} is not supported for PuttyRunner.");
        }

        /// <summary>
        /// install the runner exe to the path. if path is empty, use the default path.
        /// </summary>
        public override void Install(string path = "")
        {
            if (string.IsNullOrEmpty(path))
                path = GetExeInstallPath();
            _1RM.Utils.PuTTY.Model.Utils.Install("Resources/PuTTY/putty.exe", path);
        }


        public override string GetExeInstallPath()
        {
            string exeName = $"putty_portable_{Assert.APP_NAME}.exe";
            if (!Directory.Exists(AppPathHelper.Instance.PuttyDirPath))
                Directory.CreateDirectory(AppPathHelper.Instance.PuttyDirPath);
            var exeFullName = Path.Combine(AppPathHelper.Instance.PuttyDirPath, exeName);
            return exeFullName;
        }

        public static string GetAutoCommandFilePath(ProtocolBase protocol)
        {
            if (protocol is SSH ssh && !string.IsNullOrEmpty(ssh.StartupAutoCommand))
                return Path.Combine(Path.GetTempPath(), $"{protocol.SessionId}_putty_auto_command.txt");
            return "";
        }

        public void SaveAutoCommandFile(ProtocolBase protocol, int autoDeleteSeconds = 0)
        {
            if (protocol is SSH ssh)
            {
                // write command
                var tempFile = GetAutoCommandFilePath(protocol);
                File.WriteAllText(tempFile,
$@"# to setup environment
source /etc/profile
source ~/.bashrc
{ssh.StartupAutoCommand}
# echo ""Press any key to continue...""
# read -n 1 -s
exec $SHELL # to keep putty alive
            ");

                if (autoDeleteSeconds > 0)
                {
                    var autoDelTask = new Task(() =>
                    {
                        Thread.Sleep(autoDeleteSeconds * 1000);
                        try
                        {
                            if (File.Exists(tempFile))
                                File.Delete(tempFile);
                            SimpleLogHelper.DebugWarning($"AutoCommandFile {tempFile} is deleted!");
                        }
                        catch
                        {
                            // ignored
                        }
                    });
                    autoDelTask.Start();
                }
            }
        }

        /// <summary>
        /// return the private key path for the protocol if exists.
        /// when the key is original keep in a none-ascii path, we will copy it to a temp path and delete it after use.
        /// </summary>
        public string GetPrivateKeyPath(ProtocolBase protocol, int autoDeleteSeconds = 30)
        {
            string sshPrivateKeyPath = "";
            if (protocol is ProtocolBaseWithAddressPortUserPwd { UsePrivateKeyForConnect: true } pw && string.IsNullOrEmpty(pw.PrivateKey) == false)
            {
                sshPrivateKeyPath = pw.PrivateKey;
                // if private key is not all ascii, copy it to temp file
                if (pw.IsPrivateKeyAllAscii() == false && File.Exists(pw.PrivateKey))
                {
                    sshPrivateKeyPath = Path.Combine(Path.GetTempPath(), new FileInfo(pw.PrivateKey).Name);
                    File.Copy(pw.PrivateKey, sshPrivateKeyPath, true);
                    var autoDelTask = new Task(() =>
                    {
                        Thread.Sleep(30 * 1000);
                        try
                        {
                            if (File.Exists(sshPrivateKeyPath))
                                File.Delete(sshPrivateKeyPath);
                            SimpleLogHelper.DebugWarning($"SSH KEY {sshPrivateKeyPath} is deleted!");
                        }
                        catch
                        {
                            // ignored
                        }
                    });
                    autoDelTask.Start();
                }
            }
            return sshPrivateKeyPath;
        }


        public void ConfigPutty(IPuttyConnectable iPuttyConnectable, string sessionId, string sshPrivateKeyPath)
        {
            PuttyRunner puttyRunner = this;
            // install PUTTY if `puttyRunner.PuttyExePath` not exists
            if (string.IsNullOrEmpty(puttyRunner.ExePath) || File.Exists(puttyRunner.ExePath) == false)
            {
                puttyRunner.Install();
            }

            // create session config
            var puttyOption = new PuttyConfig(sessionId);
            puttyOption.Set(EnumConfigKey.LineCodePage, puttyRunner.GetLineCodePageForIni());
            puttyOption.ApplyOverwriteSession(iPuttyConnectable.ExternalSessionConfigPath);

            if (iPuttyConnectable is SSH server)
            {
                if (!string.IsNullOrEmpty(sshPrivateKeyPath))
                {
                    // set key
                    puttyOption.Set(EnumConfigKey.PublicKeyFile, sshPrivateKeyPath);
                }
                puttyOption.Set(EnumConfigKey.HostName, server.Address);
                puttyOption.Set(EnumConfigKey.PortNumber, server.GetPort());
                puttyOption.Set(EnumConfigKey.Protocol, "ssh");
            }
            if (iPuttyConnectable is Serial serial)
            {
                puttyOption.Set(EnumConfigKey.BackspaceIsDelete, 0);
                puttyOption.Set(EnumConfigKey.LinuxFunctionKeys, 4);

                //SerialLine\COM1\
                //SerialSpeed\9600\
                //SerialDataBits\8\
                //SerialStopHalfbits\2\
                //SerialParity\0\
                //SerialFlowControl\1\
                puttyOption.Set(EnumConfigKey.Protocol, "serial");
                puttyOption.Set(EnumConfigKey.SerialLine, serial.SerialPort);
                puttyOption.Set(EnumConfigKey.SerialSpeed, serial.GetBitRate());
                puttyOption.Set(EnumConfigKey.SerialDataBits, serial.DataBits);
                puttyOption.Set(EnumConfigKey.SerialStopHalfbits, serial.StopBits);
                puttyOption.Set(EnumConfigKey.SerialParity, serial.Parity);
                puttyOption.Set(EnumConfigKey.SerialFlowControl, serial.FlowControl);
            }

            // set theme
            var options = PuttyThemes.Themes[puttyRunner.PuttyThemeName];
            foreach (var option in options)
            {
                try
                {
                    if (Enum.TryParse(option.Key, out EnumConfigKey key))
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

            puttyOption.Set(EnumConfigKey.FontHeight, puttyRunner.PuttyFontSize);
            puttyOption.Set(EnumConfigKey.Font, puttyRunner.PuttyFontSource);
            puttyOption.SaveToConfig();
        }
    }
}
