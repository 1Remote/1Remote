using _1RM.Model.Protocol.FileTransmit.Transmitters;

namespace _1RM.Model.Protocol.FileTransmit
{
    public interface IFileTransmittable
    {
        ITransmitter GeTransmitter();
        string GetStartupPath();
    }
}
