using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ColorPickerWPF.Code;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PRM.Core.Protocol;
using PRM.Core.Protocol.FileTransmit.FTP;
using PRM.Core.Protocol.FileTransmit.SFTP;
using PRM.Core.Protocol.Putty.SSH;
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
        public Dictionary<string, string> ProtocolPropertyDescriptions { get; set; } = new Dictionary<string, string>();
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
            ProtocolPropertyDescriptions.Clear();
            var di = new DirectoryInfo(ProtocolFolderName);

            // build-in protocol
            //LoadRdp();
            LoadVnc();
            LoadSsh();
            LoadSftp();
            LoadFtp();


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

        //private void LoadRdp()
        //{
        //    var protocolName = ProtocolServerRDP.ProtocolName;
        //    var c = LoadConfig(protocolName) ?? new ProtocolConfig();
        //    c.Runners ??= new List<Runner>();
        //    if (c.Runners.Count == 0 || c.Runners[0] is InternalDefaultRunner == false)
        //    {
        //        c.Runners.RemoveAll(x => x is InternalDefaultRunner);
        //        c.Runners.Insert(0, new InternalDefaultRunner());
        //    }
        //    c.Runners.First(x => x is InternalDefaultRunner).Name = $"Internal {protocolName}";
        //    ProtocolConfigs.Add(protocolName, c);
        //    ProtocolPropertyDescriptions.Add(protocolName, OtherNameAttributeExtensions.GetOtherNamesDescription(typeof(ProtocolServerRDP)));
        //}

        private void LoadVnc()
        {
            var protocolName = ProtocolServerVNC.ProtocolName;
            var c = LoadConfig(protocolName) ?? new ProtocolConfig();
            c.Runners ??= new List<Runner>();
            if (c.Runners.Count == 0 || c.Runners[0] is InternalDefaultRunner == false)
            {
                c.Runners.RemoveAll(x => x is InternalDefaultRunner);
                c.Runners.Insert(0, new InternalDefaultRunner());
            }

            c.Runners.First(x => x is InternalDefaultRunner).Name = $"Internal {protocolName}";
            ProtocolConfigs.Add(protocolName, c);
            ProtocolPropertyDescriptions.Add(protocolName, OtherNameAttributeExtensions.GetOtherNamesDescription(typeof(ProtocolServerVNC)));
        }
        private void LoadSsh()
        {
            var protocolName = ProtocolServerSSH.ProtocolName;
            var c = LoadConfig(protocolName) ?? new ProtocolConfig();
            c.Runners ??= new List<Runner>();
            if (c.Runners.Count == 0 || c.Runners[0] is KittyRunner == false)
            {
                c.Runners.RemoveAll(x => x is KittyRunner);
                c.Runners.Insert(0, new KittyRunner());
            }

            c.Runners.First(x => x is KittyRunner).Name = $"Internal {protocolName}";
            ProtocolConfigs.Add(protocolName, c);
            ProtocolPropertyDescriptions.Add(protocolName, OtherNameAttributeExtensions.GetOtherNamesDescription(typeof(ProtocolServerSSH)));
        }

        private void LoadSftp()
        {
            var protocolName = ProtocolServerSFTP.ProtocolName;
            var c = LoadConfig(protocolName) ?? new ProtocolConfig();
            c.Runners ??= new List<Runner>();
            if (c.Runners.Count == 0 || c.Runners[0] is InternalDefaultRunner == false)
            {
                c.Runners.RemoveAll(x => x is InternalDefaultRunner);
                c.Runners.Insert(0, new InternalDefaultRunner());
            }

            c.Runners.First(x => x is InternalDefaultRunner).Name = $"Internal {protocolName}";
            ProtocolConfigs.Add(protocolName, c);
            ProtocolPropertyDescriptions.Add(protocolName, OtherNameAttributeExtensions.GetOtherNamesDescription(typeof(ProtocolServerSFTP)));
        }

        private void LoadFtp()
        {
            var protocolName = ProtocolServerFTP.ProtocolName;
            var c = LoadConfig(protocolName) ?? new ProtocolConfig();
            c.Runners ??= new List<Runner>();
            if (c.Runners.Count == 0 || c.Runners[0] is InternalDefaultRunner == false)
            {
                c.Runners.RemoveAll(x => x is InternalDefaultRunner);
                c.Runners.Insert(0, new InternalDefaultRunner());
            }

            c.Runners.First(x => x is InternalDefaultRunner).Name = $"Internal {protocolName}";
            ProtocolConfigs.Add(protocolName, c);
            ProtocolPropertyDescriptions.Add(protocolName, OtherNameAttributeExtensions.GetOtherNamesDescription(typeof(ProtocolServerFTP)));
        }

        public void Save()
        {
#if DEV
            // TODO only effect in dev mode
            foreach (var kv in ProtocolConfigs)
            {
                var protocolName = kv.Key;
                var config = kv.Value;
                var file = Path.Combine(ProtocolFolderName, protocolName, $"{protocolName}.json");
                File.WriteAllText(file, JsonConvert.SerializeObject(config, Formatting.Indented), Encoding.UTF8);
            }
#endif
        }
    }
}
