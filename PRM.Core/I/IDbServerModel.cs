using PRM.Core.Protocol;

namespace PRM.Core.I
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