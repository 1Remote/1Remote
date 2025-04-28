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
                //SimpleLogHelper.Debug($"we can not customize runner for protocol: {protocolName}");
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
        private static Tuple<bool, string, string, Dictionary<string, string>> GetStartInfo(this Runner runner, ProtocolBase protocol)
        {
            if(runner is not ExternalRunner && runner is not InternalExeRunner)
            {
            }


            string exePath = "";
            string exeArguments = "";
            var environmentVariables = new Dictionary<string, string>();
            if (runner is ExternalRunner er)
            {
                exePath = er.ExePath;
                // prepare args
                exeArguments = er.Arguments;
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

                // make environment variables
                foreach (var kv in er.EnvironmentVariables)
                {
                    environmentVariables.Add(kv.Key, OtherNameAttributeExtensions.Replace(protocol, kv.Value.Replace("%SSH_PRIVATE_KEY_PATH%", "%1RM_PRIVATE_KEY_PATH%")));
                }
            }
            else if (runner is InternalExeRunner ir)
            {
                exePath = ir.GetExeInstallPath();
                var check = WinCmdRunner.CheckFileExistsAndFullName(exePath);
                if (check.Item1 == false)
                {
                    ir.Install();
                }
                exeArguments = ir.GetExeArguments(protocol);
            }
            else
            {
                SimpleLogHelper.Error($"GetStartInfo: Runner '{runner.Name}' is not supported!");
                return new Tuple<bool, string, string, Dictionary<string, string>>(false, "", "",
                    new Dictionary<string, string>());
            }

            // check exe path exists
            var tmp = WinCmdRunner.CheckFileExistsAndFullName(exePath);
            if (tmp.Item1 == false)
            {
                MessageBoxHelper.ErrorAlert($"Exe file '{exePath}' of runner '{runner.Name}' does not existed!");
                return new Tuple<bool, string, string, Dictionary<string, string>>(false, "", "",
                    new Dictionary<string, string>());
            }
            exePath = tmp.Item2;


            // prepare args
            if (protocol is ProtocolBaseWithAddressPortUserPwd withAddressPortUserPwd)
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
                    //{"@", "%40" },
                };
                foreach (var kv in specialCharacters)
                {
                    withAddressPortUserPwd.Password = withAddressPortUserPwd.Password.Replace(kv.Key, kv.Value);
                }
            }

            return new Tuple<bool, string, string, Dictionary<string, string>>(true, exePath, exeArguments, environmentVariables);
        }

        public static HostBase GetHost(this Runner runner, ProtocolBase protocol, TabWindowView? tab = null)
        {
            Debug.Assert(runner.IsRunWithoutHosting() == false);

            if (runner is ExternalRunner er)
            {
                // custom runner
                var (isOk, exePath, exeArguments, environmentVariables) = er.GetStartInfo(protocol);
                if (isOk)
                {
                    var integrateHost = IntegrateHost.Create(protocol, runner, exePath, exeArguments, environmentVariables);
                    return integrateHost;
                }
            }
            if (runner is PuttyRunner ir)
            {
                // default runner
                var (isOk, exePath, exeArguments, environmentVariables) = ir.GetStartInfo(protocol);
                if (isOk)
                {
                    var integrateHost = IntegrateHost.Create(protocol, runner, exePath, exeArguments, environmentVariables);
                    return integrateHost;
                }
            }

            // build-in runner
            switch (protocol)
            {
                case RDP rdp:
                    {
                        var size = tab?.GetTabContentSize(ColorAndBrushHelper.ColorIsTransparent(protocol.ColorHex) == true);
                        return AxMsRdpClient09Host.Create(rdp, (int)(size?.Width ?? 0), (int)(size?.Height ?? 0));
                    }
                case VNC vnc:
                    {
                        return VncHost.Create(vnc);
                    }
                case SFTP sftp:
                    {
                        return FileTransmitHost.Create(sftp);
                    }
                case FTP ftp:
                    {
                        return FileTransmitHost.Create(ftp);
                    }
                case LocalApp app:
                    {
                        return IntegrateHost.Create(app, runner, app.GetExePath(), app.GetArguments(false));
                    }
                default:
                    break;
            }
            SimpleLogHelper.Fatal($"Host of {protocol.GetType()} is not implemented, or the runner ${runner.Name} is not supported");
            throw new NotImplementedException($"Host of {protocol.GetType()} is not implemented, or the ${runner.Name} is not supported");
        }
    }
}