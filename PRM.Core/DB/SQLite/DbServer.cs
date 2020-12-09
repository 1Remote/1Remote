using System.Collections.Generic;
using FreeSql.DataAnnotations;
using PRM.Core.Model;
using PRM.Core.Protocol;

namespace PRM.Core.DB.SQLite
{
    [Table(Name = "Server")]
    class DbServer
    {
        [Column(IsIdentity = true, IsPrimary = true)]
        public uint Id { get; set; }
        public string Protocol { get; set; } = "";
        public string ClassVersion { get; set; } = "";
        public string JsonConfigString { get; set; } = "";
    }

    static class DbServerHelperStatic
    {
        public static ProtocolServerBase ToServerBase(this DbServer s)
        {
            return ItemCreateHelper.CreateFromJsonString(s.JsonConfigString, s.Id);
        }
        public static DbServer ToDbServer(this ProtocolServerBase s)
        {
            var ret = new DbServer()
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