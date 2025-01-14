using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Model.ProtocolRunner.Default;
using _1RM.Service;
using _1RM.Utils;
using _1RM.Utils.KiTTY;
using _1RM.View.Host;
using _1RM.View.Host.ProtocolHosts;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Stylet;

namespace _1RM.Model.ProtocolRunner
{
    public static class RunnerHelper
    {
        /// <summary>
        /// get a selected runner, or default runner.
        /// </summary>
        public static Runner GetRunner(ProtocolConfigurationService protocolConfigurationService, ProtocolBase server, string protocolName, string? assignRunnerName = null)
        {
            if (protocolConfigurationService.ProtocolConfigs.TryGetValue(protocolName, out var p) == false)
            {
                return new InternalDefaultRunner(protocolName);
            }

            if (p.Runners.Count == 0)
            {
                //SimpleLogHelper.Debug($"we don't have any runner for protocol: {protocolName}");
                return new InternalDefaultRunner(protocolName);
            }

            var r = p.Runners.FirstOrDefault(x => x.Name == assignRunnerName);
            r ??= p.Runners.FirstOrDefault(x => x.Name == server.SelectedRunnerName);
            r ??= p.Runners.FirstOrDefault(x => x.Name == p.SelectedRunnerName);
            r ??= p.Runners.FirstOrDefault();
            return r ?? new InternalDefaultRunner(protocolName);
        }

        public static bool IsRunWithoutHosting(this Runner runner)
        {
            return runner is ExternalRunner { RunWithHosting: false };
        }

        public static void RunWithoutHosting(this Runner runner, ProtocolBase protocol)
        {
            if (runner is not ExternalRunner er) return;
            var (isOk, exePath, exeArguments, environmentVariables) = er.GetStartInfo(protocol);
            if (!isOk) return;

            var startInfo = new ProcessStartInfo();
            if (environmentVariables?.Count > 0)
                foreach (var kv in environmentVariables)
                {
                    if (startInfo.EnvironmentVariables.ContainsKey(kv.Key) == false)
                        startInfo.EnvironmentVariables.Add(kv.Key, kv.Value);
                    startInfo.EnvironmentVariables[kv.Key] = kv.Value;
                }

            startInfo.UseShellExecute = false;
            startInfo.FileName = exePath;
            startInfo.Arguments = exeArguments;
            var process = new Process() { StartInfo = startInfo };
            SessionControlService.AddUnHostingWatch(process, protocol);
            process.EnableRaisingEvents = true;
            process.Start();
        }


        /// <summary>
        /// return (noError?, exePath, exeArguments, environmentVariables)
        /// </summary>
        private static Tuple<bool, string, string, Dictionary<string, string>> GetStartInfo(this ExternalRunner runner, ProtocolBase protocol)
        {
            var exePath = runner.ExePath;
            var tmp = WinCmdRunner.CheckFileExistsAndFullName(exePath);
            if (tmp.Item1 == false)
            {
                MessageBoxHelper.ErrorAlert($"Exe file '{runner.ExePath}' of runner '{runner.Name}' does not existed!");
                return new Tuple<bool, string, string, Dictionary<string, string>>(false, "", "",
                    new Dictionary<string, string>());
            }
            exePath = tmp.Item2;

            // prepare args
            var exeArguments = runner.Arguments;
            if (runner is ExternalRunnerForSSH runnerForSsh)
            {
                switch (protocol)
                {
                    case SSH ssh when string.IsNullOrEmpty(ssh.PrivateKey) == false:
                    case SFTP sftp when string.IsNullOrEmpty(sftp.PrivateKey) == false:
                        var pw = protocol as ProtocolBaseWithAddressPortUserPwd;
                        // if private key is not all ascii, copy it to temp file
                        if (pw?.IsPrivateKeyAllAscii() == false && File.Exists(pw.PrivateKey))
                        {
                            var pk = Path.Combine(Path.GetTempPath(), new FileInfo(pw.PrivateKey).Name);
                            File.Copy(pw.PrivateKey, pk, true);
                            var autoDelTask = new Task(() =>
                            {
                                Thread.Sleep(30 * 1000);
                                try
                                {
                                    if (File.Exists(pk))
                                        File.Delete(pk);
                                }
                                catch
                                {
                                    // ignored
                                }
                            });
                            autoDelTask.Start();
                            pw.PrivateKey = pk;
                        }
                        exeArguments = runnerForSsh.ArgumentsForPrivateKey;
                        break;
                }
            }

            if (protocol is ProtocolBaseWithAddressPortUserPwd ppwd)
            {
                // Percent-encoding, some password may contain special characters, SFTP\XFTP need to encode them.
                // see: https://github.com/1Remote/1Remote/issues/673
                // ref: https://winscp.net/eng/docs/session_url#special
                var specialCharacters = new Dictionary<string, string>
                {
                    {"%", "%25" },
                    {";", "%3B" },
                    {":", "%3A" },
                    {" ", "%20" },
                    {"#", "%23" },
                    {"+", "%2B" },
                    {"/", "%2F" },
                    {"@", "%40" },
                };
                foreach (var kv in specialCharacters)
                {
                    ppwd.Password = ppwd.Password.Replace(kv.Key, kv.Value);
                }
            }


            // SSH_PRIVATE_KEY_PATH 改名为 1RM_PRIVATE_KEY_PATH 2023年10月12日，TODO 一年后删除此代码
            exeArguments = OtherNameAttributeExtensions.Replace(protocol, exeArguments.Replace("%SSH_PRIVATE_KEY_PATH%", "%1RM_PRIVATE_KEY_PATH%"));

            // make environment variables
            var environmentVariables = new Dictionary<string, string>();
            {
                foreach (var kv in runner.EnvironmentVariables)
                {
                    environmentVariables.Add(kv.Key, OtherNameAttributeExtensions.Replace(protocol, kv.Value.Replace("%SSH_PRIVATE_KEY_PATH%", "%1RM_PRIVATE_KEY_PATH%")));
                }
            }

            return new Tuple<bool, string, string, Dictionary<string, string>>(true, exePath, exeArguments, environmentVariables);
        }

        public static IHostBase GetHost(this Runner runner, ProtocolBase protocol, TabWindowView? tab = null)
        {
            Debug.Assert(runner.IsRunWithoutHosting() == false);

            IHostBase? ihost = null;
            Execute.OnUIThreadSync(() =>
            {
                // custom runner
                if (runner is ExternalRunner er)
                {
                    var (isOk, exePath, exeArguments, environmentVariables) = er.GetStartInfo(protocol);
                    if (isOk)
                    {
                        var integrateHost = IntegrateHost.Create(protocol, runner, exePath, exeArguments, environmentVariables);
                        ihost = integrateHost;
                        return;
                    }
                }

                // build-in runner
                switch (protocol)
                {
                    case RDP rdp:
                        {
                            System.Windows.Size? size = null;
                            if (tab != null)
                            {
                                size = tab.GetTabContentSize(ColorAndBrushHelper.ColorIsTransparent(protocol.ColorHex) == true);
                            }

                            var form = new RdpHostForm(rdp, tab == null, (int)(size?.Width ?? 800), (int)(size?.Height ?? 600));
                            if (tab != null)
                            {
                                ihost = form.AttachToHostBase(); // form call show() after AttachedHost is loaded
                            }
                            else
                            {
                                ihost = form;
                            }

                            return;
                        }
                    case IKittyConnectable kittyConnectable:
                        {
                            var kittyRunner = runner is KittyRunner kitty ? kitty : new KittyRunner(protocol.ProtocolDisplayName);
                            ihost = IntegrateHost.Create(protocol, kittyRunner, kittyRunner.PuttyExePath, "");
                            return;
                        }
                    case VNC vnc:
                        {
                            ihost = VncHost.Create(vnc);
                            return;
                        }
                    case SFTP sftp:
                        {
                            ihost = FileTransmitHost.Create(sftp);
                            return;
                        }
                    case FTP ftp:
                        {
                            ihost = FileTransmitHost.Create(ftp);
                            return;
                        }
                    case LocalApp app:
                        {
                            ihost = IntegrateHost.Create(app, runner, app.GetExePath(), app.GetArguments(false));
                            return;
                        }
                    default:
                        throw new NotImplementedException($"Host of {protocol.GetType()} is not implemented");
                }
            });
            Debug.Assert(ihost != null);
            return ihost;
        }
    }
}