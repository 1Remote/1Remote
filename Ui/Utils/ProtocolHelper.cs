using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Model.ProtocolRunner;
using _1RM.Model.ProtocolRunner.Default;
using _1RM.Service;
using _1RM.Utils.KiTTY;
using _1RM.View.Host;
using _1RM.View.Host.ProtocolHosts;
using Shawn.Utils;
using Shawn.Utils.Wpf;

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
            var process = new Process() {StartInfo = startInfo};
            SessionControlService.AddUnHostingWatch(process, protocol);
            process.EnableRaisingEvents = true;
            process.Start();
        }


        /// <summary>
        /// return (noError?, exePath, exeArguments, environmentVariables)
        /// </summary>
        private static Tuple<bool, string, string, Dictionary<string, string>> GetStartInfo(this ExternalRunner er, ProtocolBase protocol)
        {
            var exePath = er.ExePath;
            var tmp = WinCmdRunner.CheckFileExistsAndFullName(exePath);
            if (tmp.Item1 == false)
            {
                MessageBoxHelper.ErrorAlert($"Exe file '{er.ExePath}' of runner '{er.Name}' does not existed!");
                return new Tuple<bool, string, string, Dictionary<string, string>>(false, "", "",
                    new Dictionary<string, string>());
            }
            exePath = tmp.Item2;

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

            // SSH_PRIVATE_KEY_PATH 改名为 1RM_PRIVATE_KEY_PATH 2023年10月12日，TODO 一年后删除此代码
            exeArguments = OtherNameAttributeExtensions.Replace(protocol, exeArguments.Replace("%SSH_PRIVATE_KEY_PATH%", "%1RM_PRIVATE_KEY_PATH%"));

            // make environment variables
            var environmentVariables = new Dictionary<string, string>();
            {
                foreach (var kv in er.EnvironmentVariables)
                {
                    environmentVariables.Add(kv.Key, OtherNameAttributeExtensions.Replace(protocol, kv.Value.Replace("%SSH_PRIVATE_KEY_PATH%", "%1RM_PRIVATE_KEY_PATH%")));
                }
            }

            return new Tuple<bool, string, string, Dictionary<string, string>>(true, exePath, exeArguments, environmentVariables);
        }

        public static HostBase GetHost(this Runner runner, ProtocolBase protocol, TabWindowView? tab = null)
        {
            Debug.Assert(runner.IsRunWithoutHosting() == false);

            // custom runner
            if (runner is ExternalRunner er)
            {
                var (isOk, exePath, exeArguments, environmentVariables) = er.GetStartInfo(protocol);
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
                case IKittyConnectable kittyConnectable:
                    {
                        var kittyRunner = runner is KittyRunner kitty ? kitty : new KittyRunner(protocol.ProtocolDisplayName);
                        var ih = IntegrateHost.Create(protocol, kittyRunner, kittyRunner.PuttyExePath, "");
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
                        return IntegrateHost.Create(app, runner, app.GetExePath(), app.GetArguments(false));
                    }
                default:
                    throw new NotImplementedException($"Host of {protocol.GetType()} is not implemented");
            }
        }
    }
}