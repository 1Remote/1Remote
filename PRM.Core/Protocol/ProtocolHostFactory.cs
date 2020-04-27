using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using PRM.Core.Protocol.RDP;
using Shawn.Ulits.RDP;

namespace PRM.Core.Protocol
{
    public static class ProtocolHostFactory
    {
        public static ProtocolHostBase Get(ProtocolServerBase server,double width = 0,double height = 0)
        {
            if (server is ProtocolServerRDP)
            {
                var host = new AxMsRdpClient09Host(server,width,height);
                return host;
            }
            else
            {
                throw new NotImplementedException();
            }
            return null;
        }

        public static bool IsConnWithFullScreen(this ProtocolServerBase server)
        {
            if (server is ProtocolServerRDP)
            {
                var rdp = ((ProtocolServerRDP) server);
                if (rdp.RdpFullScreenFlag == ERdpFullScreenFlag.EnableFullAllScreens)
                    return true;
                return rdp.AutoSetting?.FullScreen_LastSessionIsFullScreen ?? false;
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}
