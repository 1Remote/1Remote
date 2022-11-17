using _1RM.Model.DAO;
using _1RM.Model.Protocol.Base;

namespace _1RM.Utils.PRemoteM;

internal class PRemoteMServer : IDataBaseServer
{
    public int Id { get; set; }
    public string Protocol { get; set; } = "";
    public string ClassVersion { get; set; } = "";
    public string JsonConfigString { get; set; } = "";

    public ProtocolBase? ToProtocolServerBase()
    {
        return ItemCreateHelper.CreateFromDbOrm(this);
    }

    public string GetId()
    {
        return Id.ToString();
    }

    public string GetProtocol()
    {
        return Protocol;
    }

    public string GetClassVersion()
    {
        return ClassVersion;
    }

    public string GetJson()
    {
        return JsonConfigString;
    }
}