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
