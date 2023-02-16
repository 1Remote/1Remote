using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Media3D;
using _1RM.Model.DAO.Dapper;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Model.ProtocolRunner;
using _1RM.Model.ProtocolRunner.Default;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.Utils.KiTTY;
using _1RM.View.Host;
using _1RM.View.Host.ProtocolHosts;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Stylet;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace _1RM.Utils
{
    public static class ProtocolHelper
    {
        /// <summary>
        /// get a selected runner, or default runner.
        /// </summary>
        public static Runner GetRunner(ProtocolConfigurationService protocolConfigurationService, ProtocolBase server, string protocolName, string? assignRunnerName = null)
        {
            if (protocolConfigurationService.ProtocolConfigs.ContainsKey(protocolName) == false)
            {
                //SimpleLogHelper.Debug($"we can not custom runner for protocol: {protocolName}");
                return new InternalDefaultRunner(protocolName);
            }

            var p = protocolConfigurationService.ProtocolConfigs[protocolName];
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
            if (runner.IsRunWithoutHosting()) return;
            if (runner is not ExternalRunner er) return;
            var (exePath, exeArguments, environmentVariables) = er.GetStartInfo(protocol);

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

            Task.Factory.StartNew(() =>
            {
                Thread.Sleep(1 * 1000);
                protocol.RunScriptAfterDisconnected();
            });
        }



        private static Tuple<string, string, Dictionary<string, string>> GetStartInfo(this ExternalRunner er, ProtocolBase protocol)
        {
            var exePath = er.ExePath;

            // prepare args
            var exeArguments = er.Arguments;
            if (er is ExternalRunnerForSSH runnerForSsh)
            {
                switch (protocol)
                {
                    case SSH ssh when string.IsNullOrEmpty(ssh.PrivateKey) == false:
                    case SFTP sftp when string.IsNullOrEmpty(sftp.PrivateKey) == false:
                        exeArguments = runnerForSsh.ArgumentsForPrivateKey;
                        break;
                }
            }

            // make environment variables
            var environmentVariables = new Dictionary<string, string>();
            {
                foreach (var kv in er.EnvironmentVariables)
                {
                    environmentVariables.Add(kv.Key, OtherNameAttributeExtensions.Replace(protocol, kv.Value));
                }
            }

            return new Tuple<string, string, Dictionary<string, string>>(exePath, exeArguments, environmentVariables);
        }

        public static HostBase GetHost(this Runner runner, ProtocolBase protocol, TabWindowBase? tab = null)
        {
            Debug.Assert(runner.IsRunWithoutHosting() == false);
            if (runner is ExternalRunner er)
            {
                var (exePath, exeArguments, environmentVariables) = er.GetStartInfo(protocol);
                var integrateHost = IntegrateHost.Create(protocol, exePath, exeArguments, environmentVariables);
                return integrateHost;
            }

            switch (protocol)
            {
                case RDP rdp:
                    {
                        var size = tab?.GetTabContentSize(ColorAndBrushHelper.ColorIsTransparent(protocol.ColorHex) == true);
                        return AxMsRdpClient09Host.Create(rdp, (int)(size?.Width ?? 0), (int)(size?.Height ?? 0));
                    }
                case SSH ssh:
                    {
                        var kittyRunner = new KittyRunner(ssh.ProtocolDisplayName);
                        var sessionName = $"{Assert.APP_NAME}_{ssh.Protocol}_{ssh.Id}_{DateTimeOffset.Now.ToUnixTimeSeconds()}";
                        ssh.ConfigKitty(sessionName, kittyRunner, ssh.PrivateKey);
                        var ih = IntegrateHost.Create(ssh, kittyRunner.PuttyExePath, ssh.GetExeArguments(sessionName));
                        ih.RunAfterConnected = () => PuttyConnectableExtension.DelKittySessionConfig(sessionName, kittyRunner.PuttyExePath);
                        return ih;
                    }
                case Telnet telnet:
                    {
                        var kittyRunner = new KittyRunner(telnet.ProtocolDisplayName);
                        var sessionName = $"{Assert.APP_NAME}_{telnet.Protocol}_{telnet.Id}_{DateTimeOffset.Now.ToUnixTimeSeconds()}";
                        telnet.ConfigKitty(sessionName, kittyRunner, "");
                        var ih = IntegrateHost.Create(telnet, kittyRunner.PuttyExePath, telnet.GetExeArguments(sessionName));
                        ih.RunAfterConnected = () => PuttyConnectableExtension.DelKittySessionConfig(sessionName, kittyRunner.PuttyExePath);
                        return ih;
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
                        var tmp = WinCmdRunner.CheckFileExistsAndFullName(app.ExePath);
                        return IntegrateHost.Create(app, tmp.Item2, app.Arguments);
                    }
                default:
                    throw new NotImplementedException($"Host of {protocol.GetType()} is not implemented");
            }
        }
    }
}