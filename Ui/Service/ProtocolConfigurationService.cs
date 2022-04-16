using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PRM.Model;
using PRM.Model.Protocol;
using PRM.Model.Protocol.Base;
using PRM.Model.ProtocolRunner;
using PRM.Model.ProtocolRunner.Default;
using Shawn.Utils;

namespace PRM.Service
{
    public class ProtocolConfigurationService
    {
        public Dictionary<string, ProtocolSettings> ProtocolConfigs { get; }


        private static List<string> _customProtocolBlackList = new List<string>();
        /// <summary>
        /// Protocol name in this list can not be custom
        /// </summary>
        public static List<string> CustomProtocolBlackList
        {
            get
            {
                if (_customProtocolBlackList.Count == 0)
                {
                    var protocolList = ProtocolBase.GetAllSubInstance();
                    var names = protocolList.Select(x => x.Protocol);
                    _customProtocolBlackList = names.ToList();
                }
                return _customProtocolBlackList;
            }
        }

        public readonly string ProtocolFolderName;

        /// <summary>
        /// true -> Portable mode(for exe) setting files saved in Environment.CurrentDirectory;
        /// false -> ApplicationData mode(for Microsoft store.)  setting files saved in Environment.CurrentDirectory 
        /// </summary>
        public readonly bool IsPortable;

        public ProtocolConfigurationService(bool isPortable)
        {
            IsPortable = isPortable;
            string protocolFolder2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ConfigurationService.AppName, "Protocols");

            if (IsPortable)
            {
                string protocolFolder1 = Path.Combine(Environment.CurrentDirectory, "Protocols");
                ProtocolFolderName = protocolFolder1;
                // read the protocolFolder1 first
                if (LoadCheck(protocolFolder1))
                {
                    ProtocolConfigs = Load(protocolFolder1);
                }
                // if ProtocolConfigs is empty, try to read protocolFolder2
                else if (LoadCheck(protocolFolder2))
                {
                    ProtocolConfigs = Load(protocolFolder2);
                    ProtocolFolderName = protocolFolder2;
                }
                // protocolFolder1, protocolFolder2 are empty, init ProtocolFolderName
                else
                {
                    if (Directory.Exists(ProtocolFolderName) == false)
                        Directory.CreateDirectory(ProtocolFolderName);
                    ProtocolConfigs = Load(ProtocolFolderName);
                }
            }
            else
            {
                ProtocolFolderName = protocolFolder2;
                if (Directory.Exists(ProtocolFolderName) == false)
                    Directory.CreateDirectory(ProtocolFolderName);
                ProtocolConfigs = Load(ProtocolFolderName);
            }
        }

        private static bool LoadCheck(string protocolFolderName)
        {
            if (Directory.Exists(protocolFolderName) == false)
                return false;
            var di = new DirectoryInfo(protocolFolderName);
            var fis = di.GetFiles("*.json");
            if (fis.Length == 0)
                return false;

            return true;
        }

        private static Dictionary<string, ProtocolSettings> Load(string protocolFolderName)
        {
            var protocolConfigs = new Dictionary<string, ProtocolSettings>();

            // build-in protocol
            protocolConfigs.Add(VNC.ProtocolName, InitProtocol(protocolFolderName, new VNC(), new InternalDefaultRunner(), $"Internal VNC"));
            protocolConfigs.Add(SSH.ProtocolName, InitProtocol(protocolFolderName, new SSH(), new KittyRunner(), $"Internal KiTTY"));
            protocolConfigs.Add(Telnet.ProtocolName, InitProtocol(protocolFolderName, new Telnet(), new KittyRunner(), $"Internal KiTTY"));
            protocolConfigs.Add(SFTP.ProtocolName, InitProtocol(protocolFolderName, new SFTP(), new InternalDefaultRunner(), $"Internal SFTP"));
            protocolConfigs.Add(FTP.ProtocolName, InitProtocol(protocolFolderName, new FTP(), new InternalDefaultRunner(), $"Internal FTP"));



            // custom protocol
            var di = new DirectoryInfo(protocolFolderName);
            {
                var customs = new Dictionary<string, ProtocolSettings>();
                foreach (var fi in di.GetFiles("*.json"))
                {
                    var protocolName = fi.Name.Replace(fi.Extension, "");
                    // remove existed protocol
                    if (protocolConfigs.Any(x => string.Equals(protocolName, x.Key, StringComparison.CurrentCultureIgnoreCase)))
                        continue;
                    // remove special protocol
                    if (CustomProtocolBlackList.Any(x => string.Equals(protocolName, x, StringComparison.CurrentCultureIgnoreCase)))
                        continue;
                    var c = LoadConfig(protocolFolderName, protocolName);
                    if (c != null)
                    {
                        customs.Add(protocolName, c);
                    }
                }
                foreach (var custom in customs)
                {
                    protocolConfigs.Add(custom.Key, custom.Value);
                }
            }


            // add macros to ExternalRunner
            foreach (var config in protocolConfigs)
            {
                foreach (var runner in config.Value.Runners)
                {
                    if (runner is ExternalRunner er)
                    {
                        er.MarcoNames = config.Value.MarcoNames;
                        er.ProtocolType = config.Value.ProtocolType;
                    }
                }
            }

            return protocolConfigs;
        }


        public static ProtocolSettings? LoadConfig(string protocolFolderName, string protocolName)
        {
            protocolName = protocolName.ToUpper();
            var file = Path.Combine(protocolFolderName, $"{protocolName}.json");
            if (File.Exists(file))
            {
                var jsonStr = File.ReadAllText(file, Encoding.UTF8);
                var jobj = JObject.Parse(jsonStr);
                var runners = jobj[nameof(ProtocolSettings.Runners)] as JArray;
                jobj.Remove(nameof(ProtocolSettings.Runners));
                var serializer = new JsonSerializer();
                var c = (ProtocolSettings)serializer.Deserialize(new JTokenReader(jobj), typeof(ProtocolSettings))!;

                if (runners != null)
                    foreach (var runner in runners)
                    {
                        try
                        {
                            var r = JsonConvert.DeserializeObject<Runner>(runner.ToString());
                            if (r != null)
                                c.Runners.Add(r);

                        }
                        catch (Exception e)
                        {
                            Console.Write(e);
                        }
                    }

                if (c != null)
                    return c;
            }

            return null;
        }


        private static ProtocolSettings InitProtocol<T, T2>(string protocolFolderName, T protocolBase, T2 defaultRunner, string defaultRunnerName) where T : ProtocolBase where T2 : Runner
        {
            var t = protocolBase.GetType();
            var protocolName = protocolBase.Protocol;
            var macros = OtherNameAttributeExtensions.GetOtherNames(t); // get property name for auto complete
            var c = LoadConfig(protocolFolderName, protocolName) ?? new ProtocolSettings();
            c.Init(macros.Select(x => x.Value).ToList(), macros.Select(x => x.Key).ToList(), t);
            if (c.Runners == null || c.Runners.Count == 0 || c.Runners.All(x => x is InternalDefaultRunner))
            {
                c.Runners ??= new List<Runner>();
                if (VNC.ProtocolName == protocolBase.Protocol)
                {
                    if (c.Runners.All(x => x.Name != "UltraVNC"))
                        c.Runners.Add(new ExternalRunner("UltraVNC")
                        {
                            ExePath = @"C:\Program Files (x86)\uvnc\vncviewer.exe",
                            Arguments = @"%PRM_HOSTNAME%:%PRM_PORT% -password %PRM_PASSWORD%",
                            RunWithHosting = false,
                        });
                    if (c.Runners.All(x => x.Name != "TightVNC"))
                        c.Runners.Add(new ExternalRunner("TightVNC")
                        {
                            ExePath = @"C:\Program Files\TightVNC\tvnviewer.exe",
                            Arguments = @"%PRM_HOSTNAME%::%PRM_PORT% -password=%PRM_PASSWORD% -scale=auto",
                            RunWithHosting = true,
                            EnvironmentVariables = new ObservableCollection<ExternalRunner.ObservableKvp<string, string>>(new[] { new ExternalRunner.ObservableKvp<string, string>("VNC_PASSWORD", "%PRM_PASSWORD%") }),
                        });
                }
                if (SFTP.ProtocolName == protocolBase.Protocol)
                {
                    if (c.Runners.All(x => x.Name != "WinSCP"))
                        c.Runners.Add(new ExternalRunner("WinSCP")
                        {
                            ExePath = @"C:\Program Files (x86)\WinSCP\WinSCP.exe",
                            Arguments = @"sftp://%PRM_USERNAME%:%PRM_PASSWORD%@%PRM_HOSTNAME%:%PRM_PORT%",
                        });
                }
            }
            if (c.Runners.FirstOrDefault() is InternalDefaultRunner == false)
            {
                c.Runners.RemoveAll(x => x is InternalDefaultRunner);
                c.Runners.Insert(0, defaultRunner);
            }
            c.Runners.First(x => x is InternalDefaultRunner).Name = defaultRunnerName;
            return c;
        }

        public void Save()
        {
            foreach (var kv in ProtocolConfigs)
            {
                var protocolName = kv.Key;
                var config = kv.Value;
                foreach (var runner in config.Runners.Where(x => x is ExternalRunner))
                {
                    var externalRunner = (ExternalRunner)runner;
                    foreach (var ev in externalRunner.EnvironmentVariables.ToArray().Where(x => string.IsNullOrWhiteSpace(x.Key)))
                    {
                        externalRunner.EnvironmentVariables.Remove(ev);
                    }
                }
                var file = Path.Combine(ProtocolFolderName, $"{protocolName}.json");
                File.WriteAllText(file, JsonConvert.SerializeObject(config, Formatting.Indented), Encoding.UTF8);
            }
        }
    }
}
