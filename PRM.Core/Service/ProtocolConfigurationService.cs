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

namespace PRM.Core.Service
{
    public class ProtocolConfigurationService
    {
        public Dictionary<string, ProtocolConfig> ProtocolConfigs { get; set; } = new Dictionary<string, ProtocolConfig>();

        public readonly string ProtocolFolderName;

        public ProtocolConfigurationService()
        {
            var appDateFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ConfigurationService.AppName);
            ProtocolFolderName = Path.Combine(appDateFolder, "Protocols");
            if (Directory.Exists(ProtocolFolderName) == false)
                Directory.CreateDirectory(ProtocolFolderName);
            if (Directory.Exists(Path.Combine(ProtocolFolderName, "SSH")) == false)
                Directory.CreateDirectory(Path.Combine(ProtocolFolderName, "SSH"));
            if (Directory.Exists(Path.Combine(ProtocolFolderName, "RDP")) == false)
                Directory.CreateDirectory(Path.Combine(ProtocolFolderName, "RDP"));
            if (Directory.Exists(Path.Combine(ProtocolFolderName, "VNC")) == false)
                Directory.CreateDirectory(Path.Combine(ProtocolFolderName, "VNC"));
            if (Directory.Exists(Path.Combine(ProtocolFolderName, "TELNET")) == false)
                Directory.CreateDirectory(Path.Combine(ProtocolFolderName, "TELNET"));
            if (Directory.Exists(Path.Combine(ProtocolFolderName, "FTP")) == false)
                Directory.CreateDirectory(Path.Combine(ProtocolFolderName, "FTP"));
            if (Directory.Exists(Path.Combine(ProtocolFolderName, "SFTP")) == false)
                Directory.CreateDirectory(Path.Combine(ProtocolFolderName, "SFTP"));

            Load();
        }


        public void Load()
        {
            ProtocolConfigs.Clear();
            var di = new DirectoryInfo(ProtocolFolderName);
            LoadRdp();
            LoadSsh();
            LoadSftp();
            foreach (var directoryInfo in di.GetDirectories())
            {
                var protocolName = directoryInfo.Name;
                if (ProtocolConfigs.ContainsKey(protocolName))
                    continue;

                var c = LoadConfig(protocolName);
                if (c != null)
                {
                    ProtocolConfigs.Add(protocolName, c);
                }
            }
        }

        public ProtocolConfig LoadConfig(string protocolName)
        {
            protocolName = protocolName.ToUpper();
            if (Directory.Exists(Path.Combine(ProtocolFolderName, protocolName)) == false)
                Directory.CreateDirectory(Path.Combine(ProtocolFolderName, protocolName));
            var file = Path.Combine(ProtocolFolderName, protocolName, $"{protocolName}.json");
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
                        catch
                        {
                            // ignored
                        }
                    }

                if (c != null)
                    return c;
            }

            return null;
        }

        private void LoadRdp()
        {
            var protocolName = ProtocolServerRDP.ProtocolName;
            var c = LoadConfig(protocolName) ?? new ProtocolConfig();
            if (c.Runners.Count == 0 || c.Runners[0] is InternalDefaultRunner == false)
            {
                c.Runners.RemoveAll(x => x is InternalDefaultRunner);
                c.Runners.Insert(0, new InternalDefaultRunner());
            }

            c.Runners.First(x => x is InternalDefaultRunner).Name = $"Internal {protocolName}";
            ProtocolConfigs.Add(protocolName, c);
        }

        private void LoadSsh()
        {
            var protocolName = ProtocolServerSSH.ProtocolName;
            var c = LoadConfig(protocolName) ?? new ProtocolConfig();

            if (c.Runners.Count == 0 || c.Runners[0] is KittyRunner == false)
            {
                c.Runners.RemoveAll(x => x is KittyRunner);
                c.Runners.Insert(0, new KittyRunner());
            }

            c.Runners.First(x => x is KittyRunner).Name = $"Internal {protocolName}";
            ProtocolConfigs.Add(protocolName, c);
        }

        private void LoadSftp()
        {
            var protocolName = ProtocolServerSFTP.ProtocolName;
            var c = LoadConfig(protocolName) ?? new ProtocolConfig();
            if (c.Runners.Count == 0 || c.Runners[0] is InternalDefaultRunner == false)
            {
                c.Runners.RemoveAll(x => x is InternalDefaultRunner);
                c.Runners.Insert(0, new InternalDefaultRunner());
            }

            c.Runners.First(x => x is InternalDefaultRunner).Name = $"Internal {protocolName}";
            ProtocolConfigs.Add(protocolName, c);
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
