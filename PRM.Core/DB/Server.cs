using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media.Animation;
using PRM.Core.Model;
using PRM.Core.Protocol;
using SQLite;

namespace PRM.Core.DB
{
    public class Server : OrmTableBase
    {
        public static void Init()
        {
            using (var db = GetDb())
            {
                db.CreateTable<Server>();
            }
        }

        [PrimaryKey, AutoIncrement]
        public uint Id { get; set; }

        [NotNull]
        public string Protocol { get; set; } = "";

        [NotNull]
        public string ClassVersion { get; set; } = "";

        [NotNull]
        public string DispName { get; set; } = "";

        public string GroupName { get; set; } = "";


        public void SetLastConnTime(DateTime dt)
        {
            LastConnTime = dt.ToString("yyyyMMdd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        }

        public string LastConnTime { get; private set; } = "";

        [NotNull]
        public string JsonConfigString { get; set; } = "";


        public static Server FromProtocolServerBase(ProtocolServerBase org)
        {
            var ret = new Server();
            ret.Id = org.Id;
            ret.Protocol = org.Protocol;
            ret.ClassVersion = org.ClassVersion;
            ret.DispName = org.DispName;
            ret.GroupName = org.GroupName;
            ret.JsonConfigString = org.ToJsonString();
            return ret;
        }
    }
}