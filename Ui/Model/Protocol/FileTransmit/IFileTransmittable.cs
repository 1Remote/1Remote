using PRM.Model.Protocol.FileTransmit.Transmitters;

namespace PRM.Model.Protocol.FileTransmit
{
    public interface IFileTransmittable
    {
        ITransmitter GeTransmitter();
        string GetStartupPath();
    }
}
