using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using PRM.Core.Protocol.Putty;
using PRM.Core.Protocol.Putty.Host;
using PRM.Core.Protocol.Putty.SSH;
using PRM.Core.Protocol.RDP;
using Shawn.Ulits.RDP;

namespace PRM.Core.Protocol
{
    public static class ProtocolHostFactory
    {
        public static ProtocolHostBase Get(ProtocolServerBase server, double width = 0, double height = 0)
        {
            switch (server)
            {
                case ProtocolServerRDP _:
                    {
                        var host = new AxMsRdpClient09Host(server,width,height);
                        return host;
                    }
                case ProtocolServerSSH ssh:
                    {
                        var host = new PuttyHost(ssh);
                        return host;
                    }
                default:
                    throw new NotImplementedException();
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
                case ProtocolServerSSH _:
                    {
                        return false;
                    }
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
