using System;
using PRM.Core.Model;
using PRM.Core.Protocol.FileTransmit.FTP;
using PRM.Core.Protocol.FileTransmit.Host;
using PRM.Core.Protocol.FileTransmit.SFTP;
using PRM.Core.Protocol.Putty.Host;
using PRM.Core.Protocol.Putty.SSH;
using PRM.Core.Protocol.Putty.Telnet;
using PRM.Core.Protocol.RDP;
using PRM.Core.Protocol.RDP.Host;
using PRM.Core.Protocol.VNC;
using PRM.Core.Protocol.VNC.Host;

namespace PRM.Core.Protocol
{
    public static class ProtocolHostFactory
    {
        public static ProtocolHostBase Get(PrmContext context, ProtocolServerBase server, double width = 0, double height = 0)
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
                        var host = new KittyHost(context, ssh);
                        return host;
                    }
                case ProtocolServerTelnet telnet:
                    {
                        var host = new KittyHost(context, telnet);
                        return host;
                    }
                case ProtocolServerVNC vnc:
                    {
                        var host = new VncHost(context, vnc);
                        return host;
                    }
                case ProtocolServerSFTP sftp:
                    {
                        var host = new FileTransmitHost(context, sftp);
                        return host;
                    }
                case ProtocolServerFTP ftp:
                    {
                        var host = new FileTransmitHost(context, ftp);
                        return host;
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
                        if (rdp.IsConnWithFullScreen)
                            return true;
                        return rdp.AutoSetting?.FullScreenLastSessionIsFullScreen ?? false;
                    }
                default:
                    return false;
            }
        }
    }
}