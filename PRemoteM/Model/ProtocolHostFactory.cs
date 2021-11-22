using System;
using System.Diagnostics;
using System.IO;
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
        private static Runner GetRunner(PrmContext context, string protocolName)
        {
            if (context.ProtocolConfigurationService.ProtocolConfigs.ContainsKey(protocolName) == false)
                return null;

            var p = context.ProtocolConfigurationService.ProtocolConfigs[protocolName];
            var r = p.GetRunner();
            return r;
        }
        private static HostBase TryGetCustomRunner<T>(PrmContext context, string protocolName, T psb, out bool isOk) where T : ProtocolServerBase
        {
            isOk = true;
            var r = GetRunner(context, protocolName);
            if (r is ExternalRunner er)
            {
                var exePath = er.ExePath;
                var args = er.Arguments;
                if (File.Exists(exePath))
                {
                    // using external runner.
                    //var template = $@"sftp://%PRM_USER_NAME%:%PRM_PASSWORD%@%PRM_ADDRESS%:%PRM_PORT%";
                    //var host2 = new IntegrateHost(context, sftp, @"C:\Program Files (x86)\WinSCP\WinSCP.exe", $@"sftp://{sftp.UserName}:{context.DataService.DecryptOrReturnOriginalString(sftp.Password)}@{sftp.Address}:{sftp.GetPort()}");
                    var tmpSftp = psb.Clone();
                    tmpSftp.ConnectPreprocess(context);
                    var exeArguments = OtherNameAttributeExtensions.Replace(tmpSftp, args);
                    if (er.RunWithHosting)
                    {
                        var host2 = new IntegrateHost(context, psb, exePath, exeArguments);
                        return host2;
                    }
                    else
                    {
                        Process.Start(exePath, exeArguments);
                        return null;
                    }
                }
            }

            isOk = false;
            return null;
        }

        public static HostBase Get(PrmContext context, ProtocolServerBase server, double width = 0, double height = 0)
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
                        var host1 = TryGetCustomRunner(context, ProtocolServerSSH.ProtocolName, ssh, out var isOk);
                        if (isOk)
                            return host1;

                        var r = GetRunner(context, ProtocolServerSSH.ProtocolName);
                        // KittyRunner
                        ssh.InstallKitty();
                        var host = new IntegrateHost(context, ssh, ssh.GetExeFullPath(), ssh.GetExeArguments(context));
                        // load theme for Kitty
                        if (r is KittyRunner sdr)
                        {
                            host.RunBeforeConnect = () => ssh.SetKittySessionConfig(sdr.GetPuttyFontSize(), sdr.GetPuttyThemeName(), ssh.PrivateKey);
                            host.RunAfterConnected = () => ssh.DelKittySessionConfig();
                        }
                        return host;
                    }
                case ProtocolServerTelnet telnet:
                    {
                        var host1 = TryGetCustomRunner(context, ProtocolServerTelnet.ProtocolName, telnet, out var isOk);
                        if (isOk)
                            return host1;

                        var r = GetRunner(context, ProtocolServerSSH.ProtocolName);
                        // KittyRunner
                        telnet.InstallKitty();
                        var host = new IntegrateHost(context, telnet, telnet.GetExeFullPath(), telnet.GetExeArguments(context));
                        // load theme for Kitty
                        if (r is KittyRunner sdr)
                        {
                            host.RunBeforeConnect = () => telnet.SetKittySessionConfig(14, "", "");
                            host.RunAfterConnected = () => telnet.DelKittySessionConfig();
                        }
                        return host;
                    }
                case ProtocolServerVNC vnc:
                    {
                        var host1 = TryGetCustomRunner(context, ProtocolServerVNC.ProtocolName, vnc, out var isOk);
                        if (isOk)
                            return host1;

                        var host = new VncHost(context, vnc);
                        return host;
                    }
                case ProtocolServerSFTP sftp:
                    {
                        var host1 = TryGetCustomRunner(context, ProtocolServerSFTP.ProtocolName, sftp, out var isOk);
                        if (isOk)
                            return host1;

                        var host = new FileTransmitHost(context, sftp);
                        return host;
                    }
                case ProtocolServerFTP ftp:
                    {
                        var host1 = TryGetCustomRunner(context, ProtocolServerFTP.ProtocolName, ftp, out var isOk);
                        if (isOk)
                            return host1;

                        var host = new FileTransmitHost(context, ftp);
                        return host;
                    }
                case ProtocolServerApp app:
                    {
                        if (File.Exists(app.ExePath) == false)
                        {
                            // TODO alert exe is not existed.
                            //MessageBox.Show("")
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