using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PRM.Core.Model;
using PRM.Core.Protocol.FileTransmit.Transmitters;

namespace PRM.Core.Protocol.FileTransmit
{
    public interface IProtocolFileTransmittable
    {
        ITransmitter GeTransmitter(PrmContext context);
        string GetStartupPath();
    }
}
