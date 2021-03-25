using PRM.Core.DB.IDB;
using PRM.Core.Protocol;

namespace PRM.Core.DB.Dapper
{
    public class Server : IDbServer
    {
        public int Id { get; set; }
        public string Protocol { get; set; } = "";
        public string ClassVersion { get; set; } = "";
        public string JsonConfigString { get; set; } = "";

        public ProtocolServerBase ToProtocolServerBase()
        {
            return ItemCreateHelper.CreateFromDbOrm(this);
        }

        public int GetId()
        {
            return Id;
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

    internal static class ServerHelperStatic
    {
        public static Server ToDbServer(this ProtocolServerBase s)
        {
            var ret = new Server()
            {
                Id = s.Id,
                ClassVersion = s.ClassVersion,
                JsonConfigString = s.ToJsonString(),
                Protocol = s.Protocol,
            };
            return ret;
        }
    }
}