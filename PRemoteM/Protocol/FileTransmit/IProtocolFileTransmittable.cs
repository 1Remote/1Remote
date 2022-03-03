using PRM.Model;
using PRM.Protocol.FileTransmit.Transmitters;

namespace PRM.Protocol.FileTransmit
{
    public interface IProtocolFileTransmittable
    {
        ITransmitter GeTransmitter(PrmContext context);
        string GetStartupPath();
    }
}
