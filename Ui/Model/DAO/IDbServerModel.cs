using PRM.Model.Protocol.Base;

namespace PRM.Model.DAO
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