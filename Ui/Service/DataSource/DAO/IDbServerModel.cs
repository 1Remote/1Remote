using _1RM.Model.Protocol.Base;

namespace _1RM.Service.DataSource.DAO
{
    public interface IDataBaseServer
    {
        /// <summary>
        /// ULID since 1Remote
        /// </summary>
        string GetId();

        string GetProtocol();

        string GetClassVersion();

        string GetJson();

        ProtocolBase? ToProtocolServerBase();
    }
}