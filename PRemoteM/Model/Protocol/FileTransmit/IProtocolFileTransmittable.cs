using PRM.Model.Protocol.FileTransmit.Transmitters;

namespace PRM.Model.Protocol.FileTransmit
{
    public interface IProtocolFileTransmittable
    {
        ITransmitter GeTransmitter(PrmContext context);
        string GetStartupPath();
    }
}
