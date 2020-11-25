using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PRM.Core.Protocol.FileTransmit.Transmitters;
using PRM.Core.Protocol.FileTransmitter;

namespace PRM.Core.Protocol.FileTransmit
{
    public interface IProtocolFileTransmittable
    {
        ITransmitter GeTransmitter();
        string GetStartupPath();
    }
}
