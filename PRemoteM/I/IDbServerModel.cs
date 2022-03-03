using PRM.Protocol.Base;

namespace PRM.I
{
    public interface IDbServerModel
    {
        int GetId();

        string GetProtocol();

        string GetClassVersion();

        string GetJson();

        ProtocolServerBase ToProtocolServerBase();
    }
}