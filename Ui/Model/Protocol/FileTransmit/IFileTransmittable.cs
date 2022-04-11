using PRM.Model.Protocol.FileTransmit.Transmitters;

namespace PRM.Model.Protocol.FileTransmit
{
    public interface IFileTransmittable
    {
        ITransmitter GeTransmitter(PrmContext context);
        string GetStartupPath();
    }
}
