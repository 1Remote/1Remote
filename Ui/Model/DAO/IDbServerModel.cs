using _1RM.Model.Protocol.Base;

namespace _1RM.Model.DAO
{
    public interface IDataBaseServer
    {
        int GetId();

        string GetProtocol();

        string GetClassVersion();

        string GetJson();

        ProtocolBase? ToProtocolServerBase();
    }
}