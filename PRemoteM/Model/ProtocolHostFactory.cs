using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using PRM.Core.External.KiTTY;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.Protocol.Extend;
using PRM.Core.Protocol.FileTransmit.FTP;
using PRM.Core.Protocol.FileTransmit.SFTP;
using PRM.Core.Protocol.Putty.SSH;
using PRM.Core.Protocol.Putty.Telnet;
using PRM.Core.Protocol.RDP;
using PRM.Core.Protocol.Runner;
using PRM.Core.Protocol.Runner.Default;
using PRM.Core.Protocol.VNC;
using PRM.View.ProtocolHosts;
using Shawn.Utils;
using VncHost = PRM.View.ProtocolHosts.VncHost;

namespace PRM.Model
{
    public static class ProtocolHostFactory
    {
        /// <summary>
        /// get first internal runner or first or null.
        /// </summary>
        /// <returns></returns>
        private static Runner GetDefaultRunner(PrmContext context, string protocolName)
        {
            if (context.ProtocolConfigurationService.ProtocolConfigs.ContainsKey(protocolName) == false)
                return null;
            var p = context.ProtocolConfigurationService.ProtocolConfigs[protocolName];
            if (p.Runners.Count == 0)
            {
                SimpleLogHelper.Warning($"{protocolName} does not have any runner!");
                return null;
            }
            foreach (var runner in p.Runners)
            {
                if (runner is InternalDefaultRunner)
                    return runner;
            }
            SimpleLogHelper.Warning($"{protocolName} does not have a internal runner!");
            return p.Runners.First();
        }

        /// <summary>
        /// get a host for the runner if RunWithHosting == true, or start the runner if RunWithHosting == false.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="context"></param>
        /// <param name="psb"></param>
        /// <param name="runner"></param>
        /// <returns></returns>
        private static HostBase GetHostOrRunDirectlyForExternalRunner<T>(PrmContext context, T psb, Runner runner) where T : ProtocolServerBase
        {
            if (!(runner is ExternalRunner er)) return null;

            var exePath = er.ExePath;
            var args = er.Arguments;
            if (!File.Exists(exePath))
            {
                MessageBox.Show($"Exe file '{er.ExePath}' of runner '{er.Name}' does not existed!");
                return null;
            }


            // decrypt
            var protocolServerBase = psb.Clone();
            protocolServerBase.ConnectPreprocess(context);
            var exeArguments = OtherNameAttributeExtensions.Replace(protocolServerBase, args);

            // make environment variables
            var environmentVariables = new Dictionary<string, string>();
            if (er.EnvironmentVariables != null)
                foreach (var kv in er.EnvironmentVariables)
                {
                    environmentVariables.Add(kv.Key, OtherNameAttributeExtensions.Replace(protocolServerBase, kv.Value));
                }

            // start process
            if (er.RunWithHosting)
            {
                var integrateHost = new IntegrateHost(context, psb, exePath, exeArguments, environmentVariables);
                return integrateHost;
            }
            else
            {
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
                process.Start();
                return null;
            }

        }

        /// <summary>
        /// get a selected runner, or default runner.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="context"></param>
        /// <param name="protocolName"></param>
        /// <param name="assignRunnerName"></param>
        /// <returns></returns>
        private static Runner GetRunner(PrmContext context, string protocolName, string assignRunnerName = null)
        {
            if (context.ProtocolConfigurationService.ProtocolConfigs.ContainsKey(protocolName) == false)
            {
                SimpleLogHelper.Error($"we don't have a protocol named: {protocolName}!");
                return null;
            }

            var p = context.ProtocolConfigurationService.ProtocolConfigs[protocolName];
            if (p.Runners.Count == 0)
            {
                SimpleLogHelper.Warning($"{protocolName} does not have any runner!");
                MessageBox.Show($"{protocolName} does not have any runner!");
                return null;
            }

            var runnerName = assignRunnerName;
            if (string.IsNullOrEmpty(runnerName))
                runnerName = p.SelectedRunnerName;
            var r = p.Runners.FirstOrDefault(x => x.Name == runnerName);
            if (r == null)
            {
                SimpleLogHelper.Warning($"{protocolName} does not have a runner name == the selection '{p.SelectedRunnerName}'!");
                r = p.Runners.FirstOrDefault();
            }

            return r;
        }

        /// <summary>
        /// get host for a ProtocolServerBase, can return null
        /// </summary>
        /// <param name="context"></param>
        /// <param name="server"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="assignRunnerName"></param>
        /// <returns></returns>
        public static HostBase Get(PrmContext context, ProtocolServerBase server, double width = 0, double height = 0, string assignRunnerName = null)
        {
            switch (server)
            {
                case ProtocolServerRDP _:
                    {
                        var host = new AxMsRdpClient09Host(context, server, width, height);
                        return host;
                    }
                case ProtocolServerSSH ssh:
                    {
                        var runner = GetRunner(context, ProtocolServerSSH.ProtocolName, assignRunnerName);
                        if (runner is KittyRunner kitty)
                        {
                            ssh.InstallKitty();
                            var host = new IntegrateHost(context, ssh, ssh.GetExeFullPath(), ssh.GetExeArguments(context))
                            {
                                RunBeforeConnect = () => ssh.SetKittySessionConfig(kitty.GetPuttyFontSize(), kitty.GetPuttyThemeName(), ssh.PrivateKey), 
                                RunAfterConnected = () => ssh.DelKittySessionConfig()
                            };
                            return host;
                        }
                        return GetHostOrRunDirectlyForExternalRunner(context, ssh, runner);
                    }
                case ProtocolServerTelnet telnet:
                    {
                        var runner = GetRunner(context, ProtocolServerTelnet.ProtocolName, assignRunnerName);
                        if (runner is KittyRunner kitty)
                        {
                            telnet.InstallKitty();
                            var host = new IntegrateHost(context, telnet, telnet.GetExeFullPath(), telnet.GetExeArguments(context))
                            {
                                RunBeforeConnect = () => telnet.SetKittySessionConfig(kitty.GetPuttyFontSize(), kitty.GetPuttyThemeName(), ""), 
                                RunAfterConnected = () => telnet.DelKittySessionConfig()
                            };
                            return host;
                        }

                        return GetHostOrRunDirectlyForExternalRunner(context, telnet, runner);
                    }
                case ProtocolServerVNC vnc:
                    {
                        var runner = GetRunner(context, ProtocolServerVNC.ProtocolName, assignRunnerName);
                        if (runner is InternalDefaultRunner)
                            return new VncHost(context, vnc);
                        return GetHostOrRunDirectlyForExternalRunner(context, vnc, runner);
                    }
                case ProtocolServerSFTP sftp:
                    {
                        var runner = GetRunner(context, ProtocolServerSFTP.ProtocolName, assignRunnerName);
                        if (runner is InternalDefaultRunner)
                            return new FileTransmitHost(context, sftp);
                        return GetHostOrRunDirectlyForExternalRunner(context, sftp, runner);
                    }
                case ProtocolServerFTP ftp:
                    {
                        var runner = GetRunner(context, ProtocolServerFTP.ProtocolName, assignRunnerName);
                        if (runner is InternalDefaultRunner)
                            return new FileTransmitHost(context, ftp);
                        return GetHostOrRunDirectlyForExternalRunner(context, ftp, runner);
                    }
                case ProtocolServerApp app:
                    {
                        if (File.Exists(app.ExePath) == false)
                        {
                            MessageBox.Show($"the path '{app.ExePath}' does not existed!");
                            return null;
                        }

                        if (app.RunWithHosting)
                        {
                            var host = new IntegrateHost(context, app, app.ExePath, app.Arguments);
                            return host;
                        }
                        else
                        {
                            Process.Start(app.ExePath, app.Arguments);
                        }
                        return null;
                    }
                default:
                    throw new NotImplementedException($"Host of {server.GetType()} is not implemented");
            }
        }

        public static bool IsConnWithFullScreen(this ProtocolServerBase server)
        {
            switch (server)
            {
                case ProtocolServerRDP rdp:
                    {
                        if (rdp.RdpFullScreenFlag == ERdpFullScreenFlag.EnableFullAllScreens)
                            return true;
                        if (rdp.IsConnWithFullScreen == true)
                            return true;
                        return rdp.AutoSetting?.FullScreenLastSessionIsFullScreen ?? false;
                    }
                default:
                    return false;
            }
        }
    }
}