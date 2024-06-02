using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Model.ProtocolRunner;
using _1RM.Model.ProtocolRunner.Default;
using Shawn.Utils;
using _1RM.Utils;

namespace _1RM.Service
{
    public class ProtocolConfigurationService
    {
        public Dictionary<string, ProtocolSettings> ProtocolConfigs { get; } = new Dictionary<string, ProtocolSettings>();


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


        public ProtocolConfigurationService()
        {
            if (Directory.Exists(AppPathHelper.Instance.ProtocolRunnerDirPath) == false)
                Directory.CreateDirectory(AppPathHelper.Instance.ProtocolRunnerDirPath);
            Load(AppPathHelper.Instance.ProtocolRunnerDirPath);
        }


        private void Load(string protocolFolderName)
        {
            // build-in protocol
            //ProtocolConfigs.Add(RDP.ProtocolName, InitProtocol(protocolFolderName, new RDP(), new InternalDefaultRunner(RDP.ProtocolName), $"Built-in AxMsRdpClient"));
            //ProtocolConfigs[RDP.ProtocolName].Runners.Clear();
            //ProtocolConfigs[RDP.ProtocolName].Runners.Add(new Runner("Built-in AxMsRdpClient", RDP.ProtocolName));
            //ProtocolConfigs[RDP.ProtocolName].Runners.Add(new Runner("Mstsc.exe", RDP.ProtocolName));
            ProtocolConfigs.Add(VNC.ProtocolName, InitProtocol(protocolFolderName, new VNC(), new InternalDefaultRunner(VNC.ProtocolName), $"Built-in VNC"));
            ProtocolConfigs.Add(SSH.ProtocolName, InitProtocol(protocolFolderName, new SSH(), new KittyRunner(SSH.ProtocolName), $"Built-in KiTTY"));
            ProtocolConfigs.Add(Telnet.ProtocolName, InitProtocol(protocolFolderName, new Telnet(), new KittyRunner(Telnet.ProtocolName), $"Built-in KiTTY"));
            ProtocolConfigs.Add(SFTP.ProtocolName, InitProtocol(protocolFolderName, new SFTP(), new InternalDefaultRunner(SFTP.ProtocolName), $"Built-in SFTP"));
            ProtocolConfigs.Add(FTP.ProtocolName, InitProtocol(protocolFolderName, new FTP(), new InternalDefaultRunner(FTP.ProtocolName), $"Built-in FTP"));


            // add macros to ExternalRunner
            foreach (var config in ProtocolConfigs)
            {
                var protocolName = config.Key;
                foreach (var runner in config.Value.Runners)
                {
                    if (runner is ExternalRunner er)
                    {
                        er.MarcoNames = config.Value.MarcoNames;
                        runner.OwnerProtocolName = protocolName;
                    }
                }
            }

            // SSH_PRIVATE_KEY_PATH 改名为 1RM_PRIVATE_KEY_PATH 2023年10月12日，TODO 一年后删除此代码
            var kys = new Dictionary<string, string>()
            {
                { "%SSH_PRIVATE_KEY_PATH%", "%1RM_PRIVATE_KEY_PATH%" },
                { "%HOSTNAME%", "%1RM_HOSTNAME%" },
                { "%PORT%", "%1RM_PORT%" },
                { "%USERNAME%", "%1RM_USERNAME%" },
                { "%PASSWORD%", "%1RM_PASSWORD%" },
                { "%PRIVATE_KEY_PATH%", "%1RM_PRIVATE_KEY_PATH%" },
            };

            bool saveFlag = false;
            foreach (var config in ProtocolConfigs)
            {
                var protocolName = config.Key;
                foreach (var runner in config.Value.Runners)
                {
                    if (runner is ExternalRunner er)
                    {
                        foreach (var ky in kys)
                        {
                            if (er.Arguments.IndexOf(ky.Key, StringComparison.Ordinal) >= 0)
                            {
                                saveFlag = true;
                                er.Arguments = er.Arguments.Replace(ky.Key, ky.Value);
                            }

                            foreach (var p in er.Params.Keys)
                            {
                                if (er.Params[p].IndexOf(ky.Key, StringComparison.Ordinal) >= 0)
                                {
                                    saveFlag = true;
                                    er.Params[p] = er.Params[p].Replace(ky.Key, ky.Value);
                                }
                            }

                            foreach (var t in er.EnvironmentVariables)
                            {
                                if (t.Value.IndexOf(ky.Key, StringComparison.Ordinal) >= 0)
                                {
                                    saveFlag = true;
                                    t.Value = t.Value.Replace(ky.Key, ky.Value);
                                }
                            }
                        }
                    }
                }
            }
            if (saveFlag)
            {
                Save();
            }
        }


        public static ProtocolSettings? LoadConfig(string protocolFolderName, string protocolName)
        {
            protocolName = protocolName.ToUpper();
            var file = Path.Combine(protocolFolderName, $"{protocolName}.json");
            if (File.Exists(file))
            {
                var jsonStr = File.ReadAllText(file, Encoding.UTF8);
                var c = JsonConvert.DeserializeObject<ProtocolSettings>(jsonStr);
                if (c != null)
                {
                    foreach (var runner in c.Runners)
                    {
                        runner.OwnerProtocolName = protocolName;
                    }

                    return c;
                }
            }

            return null;
        }


        private static ProtocolSettings InitProtocol<T, T2>(string protocolFolderName, T protocolBase, T2 defaultRunner, string defaultRunnerName) where T : ProtocolBase where T2 : Runner
        {
            var protocolName = protocolBase.Protocol;
            var macros = OtherNameAttributeExtensions.GetOtherNames(protocolBase.GetType()); // get property name for auto complete
            var c = LoadConfig(protocolFolderName, protocolName) ?? new ProtocolSettings();
            c.Init(macros.Select(x => x.Value).ToList(), macros.Select(x => x.Key).ToList());
            if (c.Runners.Count == 0 || c.Runners.All(x => x is InternalDefaultRunner))
            {
                c.Runners ??= new List<Runner>();
                if (VNC.ProtocolName == protocolName)
                {
                    if (c.Runners.All(x => x.Name != "UltraVNC"))
                        c.Runners.Add(new ExternalRunner("UltraVNC", protocolName)
                        {
                            ExePath = @"C:\Program Files (x86)\uvnc\vncviewer.exe",
                            Arguments = @"%1RM_HOSTNAME%:%1RM_PORT% -password %1RM_PASSWORD%",
                            RunWithHosting = false,
                        });
                    if (c.Runners.All(x => x.Name != "TightVNC"))
                        c.Runners.Add(new ExternalRunner("TightVNC", protocolName)
                        {
                            ExePath = @"C:\Program Files\TightVNC\tvnviewer.exe",
                            Arguments = @"%1RM_HOSTNAME%::%1RM_PORT% -password=%1RM_PASSWORD% -scale=auto",
                            RunWithHosting = true,
                            EnvironmentVariables = new ObservableCollection<ExternalRunner.ObservableKvp<string, string>>(new[] { new ExternalRunner.ObservableKvp<string, string>("VNC_PASSWORD", "%1RM_PASSWORD%") }),
                        });
                }
                if (SFTP.ProtocolName == protocolName)
                {
                    if (c.Runners.All(x => x.Name != "WinSCP"))
                        c.Runners.Add(new ExternalRunnerForSSH("WinSCP", protocolName)
                        {
                            ExePath = @"C:\Program Files (x86)\WinSCP\WinSCP.exe",
                            Arguments = @"sftp://%1RM_USERNAME%:%1RM_PASSWORD%@%1RM_HOSTNAME%:%1RM_PORT%",
                            ArgumentsForPrivateKey = @"sftp://%1RM_USERNAME%@%1RM_HOSTNAME%:%1RM_PORT% /privatekey=%1RM_PRIVATE_KEY_PATH%",
                        });
                }
                if (SSH.ProtocolName == protocolName)
                {
                    if (c.Runners.All(x => x.Name != "Putty"))
                        c.Runners.Add(new ExternalRunnerForSSH("Putty", protocolName)
                        {
                            ExePath = @"D:\PuTTY.exe",
                            Arguments = @"-ssh %1RM_HOSTNAME% -P %1RM_PORT% -l %1RM_USERNAME% -pw %1RM_PASSWORD% -%SSH_VERSION% -cmd ""%STARTUP_AUTO_COMMAND%""",
                            ArgumentsForPrivateKey = @"-w 1 new-tab --title ""%1RM_HOSTNAME%"" --suppressApplicationTitle plink -ssh %1RM_HOSTNAME% -P %1RM_PORT% -%SSH_VERSION% -C -X -no-antispoof -l %1RM_USERNAME% -i %1RM_PRIVATE_KEY_PATH%",
                        });
                    if (c.Runners.All(x => x.Name != "Windows Terminal"))
                        c.Runners.Add(new ExternalRunnerForSSH("Windows Terminal", protocolName)
                        {
                            ExePath = @"wt.exe",
                            Arguments = @"-w 1 new-tab --title ""%1RM_HOSTNAME%"" --suppressApplicationTitle plink -ssh %1RM_HOSTNAME% -P %1RM_PORT% -%SSH_VERSION% -C -X -no-antispoof -l %1RM_USERNAME% -pw %1RM_PASSWORD%",
                            ArgumentsForPrivateKey = @"-w 1 new-tab --title ""%1RM_HOSTNAME%"" --suppressApplicationTitle plink -ssh %1RM_HOSTNAME% -P %1RM_PORT% -%SSH_VERSION% -C -X -no-antispoof -l %1RM_USERNAME% -i %1RM_PRIVATE_KEY_PATH%",
                        });
                }
            }
            if (c.Runners.FirstOrDefault() is InternalDefaultRunner == false)
            {
                c.Runners.RemoveAll(x => x is InternalDefaultRunner);
                c.Runners.Insert(0, defaultRunner);
            }
            // 最后赋值，确保无论是从配置加载的还是上面初始化的 InternalDefaultRunner 名称正确
            c.Runners.First(x => x is InternalDefaultRunner).Name = defaultRunnerName;
            return c;
        }

        public void Save()
        {
            foreach (var kv in ProtocolConfigs)
            {
                var protocolName = kv.Key;
                var config = kv.Value;
                var file = Path.Combine(AppPathHelper.Instance.ProtocolRunnerDirPath, $"{protocolName}.json");
                RetryHelper.Try(() =>
                {
                    File.WriteAllText(file, JsonConvert.SerializeObject(config, Formatting.Indented), Encoding.UTF8);
                }, actionOnError: exception => MsAppCenterHelper.Error(exception));
            }
        }
    }
}
