using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using ColorPickerWPF.Code;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PRM.Core.Protocol;
using PRM.Core.Protocol.FileTransmit.FTP;
using PRM.Core.Protocol.FileTransmit.SFTP;
using PRM.Core.Protocol.Putty.SSH;
using PRM.Core.Protocol.Putty.Telnet;
using PRM.Core.Protocol.RDP;
using PRM.Core.Protocol.Runner;
using PRM.Core.Protocol.Runner.Default;
using PRM.Core.Protocol.VNC;
using Shawn.Utils;

namespace PRM.Core.Service
{
    public class ProtocolConfigurationService
    {
        public Dictionary<string, ProtocolConfig> ProtocolConfigs { get; set; } = new Dictionary<string, ProtocolConfig>();
        public string[] CustomProtocolBlackList => new string[] { "SSH", "RDP", "VNC", "TELNET", "FTP", "SFTP", "RemoteApp", "APP" };

        public readonly string ProtocolFolderName;

        public ProtocolConfigurationService()
        {
            // TODO 绿色版和安装版使用不同的路径，日志系统也需如此修改
            var appDateFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ConfigurationService.AppName);
            ProtocolFolderName = Path.Combine(appDateFolder, "Protocols");
            if (Directory.Exists(ProtocolFolderName) == false)
                Directory.CreateDirectory(ProtocolFolderName);
            Load();
        }


        private void Load()
        {
            ProtocolConfigs.Clear();
            var di = new DirectoryInfo(ProtocolFolderName);

            // build-in protocol

            InitProtocol(new ProtocolServerVNC(), new InternalDefaultRunner(), $"Internal VNC");
            InitProtocol(new ProtocolServerSSH(), new KittyRunner(), $"Internal KiTTY");
            InitProtocol(new ProtocolServerTelnet(), new KittyRunner(), $"Internal KiTTY");
            InitProtocol(new ProtocolServerSFTP(), new InternalDefaultRunner(), $"Internal SFTP");
            InitProtocol(new ProtocolServerFTP(), new InternalDefaultRunner(), $"Internal FTP");


            // custom protocol
            {
                var customs = new Dictionary<string, ProtocolConfig>();
                foreach (var directoryInfo in di.GetDirectories())
                {
                    var protocolName = directoryInfo.Name;
                    if (ProtocolConfigs.ContainsKey(protocolName))
                        continue;

                    var c = LoadConfig(protocolName);
                    if (c != null)
                    {
                        customs.Add(protocolName, c);
                    }
                }

                // remove special protocol
                foreach (var name in CustomProtocolBlackList)
                {
                    if (customs.Any(kv => String.Equals(kv.Key, name, StringComparison.CurrentCultureIgnoreCase)))
                    {
                        customs.Remove(name);
                    }
                }

                foreach (var custom in customs)
                {
                    ProtocolConfigs.Add(custom.Key, custom.Value);
                }
            }


            // ExternalRunner + macros
            foreach (var config in ProtocolConfigs)
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
        }


        public ProtocolConfig LoadConfig(string protocolName)
        {
            protocolName = protocolName.ToUpper();
            var file = Path.Combine(ProtocolFolderName, $"{protocolName}.json");
            if (File.Exists(file))
            {
                var jsonStr = File.ReadAllText(file, Encoding.UTF8);
                var jobj = JObject.Parse(jsonStr);
                var runners = jobj[nameof(ProtocolConfig.Runners)] as JArray;
                jobj.Remove(nameof(ProtocolConfig.Runners));
                var serializer = new JsonSerializer();
                var c = (ProtocolConfig)serializer.Deserialize(new JTokenReader(jobj), typeof(ProtocolConfig));

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


        private void InitProtocol<T, T2>(T protocolBase, T2 defaultRunner, string defaultRunnerName) where T : ProtocolServerBase where T2 : Runner
        {
            var t = protocolBase.GetType();
            var protocolName = protocolBase.Protocol;
            var macros = OtherNameAttributeExtensions.GetOtherNames(t);
            var c = LoadConfig(protocolName) ?? new ProtocolConfig();
            c.Init(macros.Select(x => x.Value).ToList(), macros.Select(x => x.Key).ToList(), t);
            c.Runners ??= new List<Runner>();
            var runnerType = defaultRunner.GetType();
            if (c.Runners.FirstOrDefault() is InternalDefaultRunner == false)
            {
                c.Runners.RemoveAll(x => x is InternalDefaultRunner);
                c.Runners.Insert(0, defaultRunner);
            }
            c.Runners.First(x => x is InternalDefaultRunner).Name = defaultRunnerName;
            ProtocolConfigs.Add(protocolName, c);
        }

        public bool Check()
        {
            return true;
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
