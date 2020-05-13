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
            var tmp = (ProtocolServerBase)org.Clone();
            tmp.SetNotifyPropertyChangedEnabled(false);
            var rsa = SystemConfig.GetInstance().DataSecurity.Rsa;
            if (rsa != null)
            {
                Debug.Assert(rsa.DecodeOrNull(tmp.DispName) == null);
                tmp.DispName = rsa.Encode(tmp.DispName);
                tmp.GroupName = rsa.Encode(tmp.GroupName);
                switch (tmp)
                {
                    case ProtocolServerNone _:
                        break;
                    case ProtocolServerRDP _:
                    case ProtocolServerSSH _:
                        var p = (ProtocolServerWithAddrPortUserPwdBase) tmp;
                        if (!string.IsNullOrEmpty(p.UserName))
                            p.UserName = rsa.Encode(p.UserName);
                        if (!string.IsNullOrEmpty(p.Address))
                            p.Address = rsa.Encode(p.Address);
                        if (!string.IsNullOrEmpty(p.Password))
                            p.Password = rsa.Encode(p.Password);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException($"Protocol not support");
                }
            }

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
            var s = new Server(){Id = id};
            return s.Delete();
        }

        public static IEnumerable<ProtocolServerBase> ListAllProtocolServerBase()
        {
            var servers = ListAll<Server>();
            var rsa = SystemConfig.GetInstance().DataSecurity.Rsa;
            foreach (var server in servers)
            {
                var tmp = ServerFactory.GetInstance().CreateFromDbObjectServerOrm(server, rsa);
                if (rsa != null)
                {
                    Debug.Assert(rsa.DecodeOrNull(tmp.DispName) != null);
                    tmp.DispName = rsa.DecodeOrNull(tmp.DispName);
                    tmp.GroupName = rsa.DecodeOrNull(tmp.GroupName);
                    switch (tmp)
                    {
                        case ProtocolServerNone _:
                            break;
                        case ProtocolServerRDP _:
                        case ProtocolServerSSH _:
                            var p = (ProtocolServerWithAddrPortUserPwdBase) tmp;
                            if (!string.IsNullOrEmpty(p.UserName))
                                p.UserName = rsa.DecodeOrNull(p.UserName);
                            if (!string.IsNullOrEmpty(p.Address))
                                p.Address = rsa.DecodeOrNull(p.Address);
                            if (!string.IsNullOrEmpty(p.Password))
                                p.Password = rsa.DecodeOrNull(p.Password);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException($"Protocol not support");
                    }
                }
                yield return tmp;
            }
        }
    }
}