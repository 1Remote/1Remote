using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Model.ProtocolRunner;
using _1RM.Model.ProtocolRunner.Default;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.Utils.KiTTY;
using _1RM.View.Host.ProtocolHosts;
using Shawn.Utils;
using Shawn.Utils.Wpf;

namespace _1RM.Utils
{
    public static class ProtocolRunnerHostHelper
    {
        /// <summary>
        /// get a selected runner, or default runner. some protocol i.e. 'APP' will return null.
        /// </summary>
        /// <param name="protocolConfigurationService"></param>
        /// <param name="server"></param>
        /// <param name="protocolName"></param>
        /// <param name="assignRunnerName"></param>
        /// <returns></returns>
        public static Runner GetRunner(ProtocolConfigurationService protocolConfigurationService, ProtocolBase server, string protocolName, string? assignRunnerName = null)
        {
            if (protocolConfigurationService.ProtocolConfigs.ContainsKey(protocolName) == false)
            {
                SimpleLogHelper.Warning($"we don't have a protocol named: {protocolName}");
                return new InternalDefaultRunner(protocolName);
            }

            var p = protocolConfigurationService.ProtocolConfigs[protocolName];
            if (p.Runners.Count == 0)
            {
                SimpleLogHelper.Warning($"{protocolName} does not have any runner!");
                return new InternalDefaultRunner(protocolName);
            }

            var r = p.Runners.FirstOrDefault(x => x.Name == assignRunnerName);
            r ??= p.Runners.FirstOrDefault(x => x.Name == server.SelectedRunnerName);
            r ??= p.Runners.FirstOrDefault(x => x.Name == p.SelectedRunnerName);
            r ??= p.Runners.FirstOrDefault();
            return r ?? new InternalDefaultRunner(protocolName);
        }

        /// <summary>
        /// get a host for the runner if RunWithHosting == true, or start the runner if RunWithHosting == false.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sourceService"></param>
        /// <param name="protocolServerBase"></param>
        /// <param name="runner"></param>
        /// <returns></returns>
        public static HostBase? GetHostOrRunDirectlyForExternalRunner<T>(DataSourceService sourceService, T protocolServerBase, Runner runner) where T : ProtocolBase
        {
            if (runner is not ExternalRunner er) return null;

            var exePath = er.ExePath;
            var args = er.Arguments;
            if (runner is ExternalRunnerForSSH runnerForSsh)
            {
                switch (protocolServerBase)
                {
                    case SSH ssh when string.IsNullOrEmpty(ssh.PrivateKey) == false:
                    case SFTP sftp when string.IsNullOrEmpty(sftp.PrivateKey) == false:
                        args = runnerForSsh.ArgumentsForPrivateKey;
                        break;
                }
            }

            var tmp = WinCmdRunner.CheckFileExistsAndFullName(exePath);
            if (tmp.Item1 == false)
            {
                MessageBoxHelper.ErrorAlert($"Exe file '{er.ExePath}' of runner '{er.Name}' does not existed!");
                return null;
            }
            exePath = tmp.Item2;


            // make exeArguments and environment variables
            string exeArguments;
            var environmentVariables = new Dictionary<string, string>();
            {
                exeArguments = OtherNameAttributeExtensions.Replace(protocolServerBase, args);
                foreach (var kv in er.EnvironmentVariables)
                {
                    environmentVariables.Add(kv.Key, OtherNameAttributeExtensions.Replace(protocolServerBase, kv.Value));
                }
            }

            // start process
            if (er.RunWithHosting)
            {
                var integrateHost = new IntegrateHost(protocolServerBase, exePath, exeArguments, environmentVariables);
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

        public static HostBase GetRdpInternalHost(ProtocolBase server, Runner runner, double width = 0, double height = 0)
        {
            Debug.Assert(runner is InternalDefaultRunner);
            Debug.Assert(server is RDP);
            var host = new AxMsRdpClient09Host((RDP)server, (int)width, (int)height);
            return host;
        }

        /// <summary>
        /// get host for a ProtocolBase, can return null
        /// </summary>
        /// <returns></returns>
        public static HostBase? GetHostForInternalRunner(DataSourceBase dataSource, ProtocolBase server, Runner runner)
        {
            Debug.Assert(runner is InternalDefaultRunner);
            switch (server)
            {
                case RDP:
                    {
                        return GetRdpInternalHost(server, runner);
                    }
                case SSH ssh:
                    {
                        var sessionName = $"{AppPathHelper.APP_NAME}_{ssh.Protocol}_{ssh.Id}_{DateTimeOffset.Now.ToUnixTimeSeconds()}";
                        if (runner is KittyRunner kitty)
                        {
                            ssh.InstallKitty();
                            ssh.SetKittySessionConfig(sessionName, kitty.GetPuttyFontSize(), kitty.PuttyThemeName, ssh.PrivateKey);
                        }
                        var host = new IntegrateHost(ssh, ssh.GetExeFullPath(), ssh.GetExeArguments(dataSource, sessionName))
                        {
                            RunAfterConnected = () => PuttyConnectableExtension.DelKittySessionConfig(sessionName)
                        };
                        return host;
                    }
                case Telnet telnet:
                    {
                        var sessionName = $"{AppPathHelper.APP_NAME}_{telnet.Protocol}_{telnet.Id}_{DateTimeOffset.Now.ToUnixTimeSeconds()}";
                        if (runner is KittyRunner kitty)
                        {
                            telnet.InstallKitty();
                            telnet.SetKittySessionConfig(sessionName, kitty.GetPuttyFontSize(), kitty.PuttyThemeName, "");
                        }
                        var host = new IntegrateHost(telnet, telnet.GetExeFullPath(), telnet.GetExeArguments(dataSource, sessionName))
                        {
                            RunAfterConnected = () => PuttyConnectableExtension.DelKittySessionConfig(sessionName)
                        };
                        return host;
                    }
                case VNC vnc:
                    {
                        return new VncHost(vnc);
                    }
                case SFTP sftp:
                    {
                        return new FileTransmitHost(sftp);
                    }
                case FTP ftp:
                    {
                        return new FileTransmitHost(ftp);
                    }
                case LocalApp app:
                    {
                        var tmp = WinCmdRunner.CheckFileExistsAndFullName(app.ExePath);
                        if (tmp.Item1 == false)
                        {
                            MessageBoxHelper.ErrorAlert($"the path '{app.ExePath}' does not existed!");
                            return null;
                        }

                        if (app.RunWithHosting)
                        {
                            var host = new IntegrateHost(app, tmp.Item2, app.Arguments);
                            return host;
                        }
                        else
                        {
                            Process.Start(tmp.Item2, app.Arguments);
                        }
                        return null;
                    }
                default:
                    throw new NotImplementedException($"Host of {server.GetType()} is not implemented");
            }
        }
    }
}