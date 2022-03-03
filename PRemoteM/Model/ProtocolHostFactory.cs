using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using PRM.Model.Protocol.Base;
using PRM.Model.Protocol.Extend;
using PRM.Model.Protocol.FileTransmit;
using PRM.Model.Protocol.Putty;
using PRM.Model.Protocol.RDP;
using PRM.Model.Protocol.Runner;
using PRM.Model.Protocol.Runner.Default;
using PRM.Model.Protocol.VNC;
using PRM.Utils.KiTTY;
using PRM.View.Host.ProtocolHosts;
using Shawn.Utils;
using VncHost = PRM.View.Host.ProtocolHosts.VncHost;

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
        /// <param name="protocolServerBase"></param>
        /// <param name="runner"></param>
        /// <returns></returns>
        public static HostBase GetHostOrRunDirectlyForExternalRunner<T>(PrmContext context, T protocolServerBase, Runner runner) where T : ProtocolServerBase
        {
            if (!(runner is ExternalRunner er)) return null;

            var exePath = er.ExePath;
            var args = er.Arguments;
            if (!File.Exists(exePath))
            {
                MessageBox.Show($"Exe file '{er.ExePath}' of runner '{er.Name}' does not existed!");
                return null;
            }


            // make exeArguments and environment variables
            var exeArguments = "";
            var environmentVariables = new Dictionary<string, string>();
            {
                exeArguments = OtherNameAttributeExtensions.Replace(protocolServerBase, args);

                if (er.EnvironmentVariables != null)
                    foreach (var kv in er.EnvironmentVariables)
                    {
                        environmentVariables.Add(kv.Key, OtherNameAttributeExtensions.Replace(protocolServerBase, kv.Value));
                    }
            }

            // start process
            if (er.RunWithHosting)
            {
                var integrateHost = new IntegrateHost(context, protocolServerBase, exePath, exeArguments, environmentVariables);
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
        /// get a selected runner, or default runner. some protocol i.e. 'APP' will return null.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="context"></param>
        /// <param name="protocolName"></param>
        /// <param name="assignRunnerName"></param>
        /// <returns></returns>
        public static Runner GetRunner(PrmContext context, string protocolName, string assignRunnerName = null)
        {
            if (context.ProtocolConfigurationService.ProtocolConfigs.ContainsKey(protocolName) == false)
            {
                SimpleLogHelper.Info($"we don't have a custom protocol named: {protocolName}!");
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
        /// <param name="runner"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public static HostBase GetHostForInternalRunner(PrmContext context, ProtocolServerBase server, Runner runner, double width = 0, double height = 0)
        {
            Debug.Assert(runner is InternalDefaultRunner || runner == null);
            switch (server)
            {
                case ProtocolServerRDP _:
                    {
                        var host = new AxMsRdpClient09Host(context, server, width, height);
                        return host;
                    }
                case ProtocolServerSSH ssh:
                    {
                        Debug.Assert(runner is KittyRunner);
                        var kitty = (KittyRunner)runner;
                        ssh.InstallKitty();
                        var host = new IntegrateHost(context, ssh, ssh.GetExeFullPath(), ssh.GetExeArguments(context))
                        {
                            RunBeforeConnect = () => ssh.SetKittySessionConfig(kitty.GetPuttyFontSize(), kitty.GetPuttyThemeName(), ssh.PrivateKey),
                            RunAfterConnected = () => ssh.DelKittySessionConfig()
                        };
                        return host;
                    }
                case ProtocolServerTelnet telnet:
                    {
                        Debug.Assert(runner is KittyRunner);
                        var kitty = (KittyRunner)runner;
                        telnet.InstallKitty();
                        var host = new IntegrateHost(context, telnet, telnet.GetExeFullPath(), telnet.GetExeArguments(context))
                        {
                            RunBeforeConnect = () => telnet.SetKittySessionConfig(kitty.GetPuttyFontSize(), kitty.GetPuttyThemeName(), ""),
                            RunAfterConnected = () => telnet.DelKittySessionConfig()
                        };
                        return host;
                    }
                case ProtocolServerVNC vnc:
                    {
                        return new VncHost(context, vnc);
                    }
                case ProtocolServerSFTP sftp:
                    {
                        return new FileTransmitHost(context, sftp);
                    }
                case ProtocolServerFTP ftp:
                    {
                        return new FileTransmitHost(context, ftp);
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
    }
}