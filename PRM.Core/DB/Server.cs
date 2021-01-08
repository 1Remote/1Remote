using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Media.Animation;
using Newtonsoft.Json;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.Protocol.Putty.SSH;
using PRM.Core.Protocol.RDP;
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
        public string JsonConfigString { get; set; } = "";


        private static Server FromProtocolServerBase(ProtocolServerBase org)
        {
            var ret = new Server
            {
                Id = org.Id,
                Protocol = org.Protocol,
                ClassVersion = org.ClassVersion,
                JsonConfigString = org.ToJsonString()
            };
            return ret;
        }

        public static int AddOrUpdate(ProtocolServerBase org, bool isAdd = false)
        {
            SystemConfig.Instance.DataSecurity.EncryptPwd(org);
            Init();
            var tmp = (ProtocolServerBase)org.Clone();
            tmp.SetNotifyPropertyChangedEnabled(false);
            SystemConfig.Instance.DataSecurity.EncryptInfo(tmp);

            if (isAdd == false)
            {
                var s = FromProtocolServerBase(tmp);
                return s.Update() ? 1 : 0;
            }
            else
            {
                var s = FromProtocolServerBase(tmp);
                return s.Insert();
            }
        }

        public static bool Delete(uint id)
        {
            Init();
            var s = new Server(){Id = id};
            return s.Delete();
        }

        public static IEnumerable<ProtocolServerBase> ListAllProtocolServerBase()
        {
            Init();
            var servers = ListAll<Server>();
            foreach (var server in servers)
            {
                var tmp = ItemCreateHelper.CreateFromDbOrm(server);
                SystemConfig.Instance.DataSecurity.DecryptInfo(tmp);
                yield return tmp;
            }
        }
    }
}