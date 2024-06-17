using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service.DataSource.DAO;
// ReSharper disable once InconsistentlySynchronizedField
// ReSharper disable InconsistentlySynchronizedField

namespace _1RM.Utils
{
    public class ItemCreateHelper
    {
        private static readonly object Locker = new object();
        private static List<ProtocolBase> _baseList = new List<ProtocolBase>();

        public static ProtocolBase? CreateFromDbOrm(IDataBaseServer iDbServer)
        {
            // reflect all the child class
            if (_baseList.Count == 0)
                lock (Locker)
                {
                    if (_baseList.Count == 0)
                    {
                        var assembly = typeof(ProtocolBase).Assembly;
                        var types = assembly.GetTypes();
                        _baseList = types.Where(x => x.IsSubclassOf(typeof(ProtocolBase)) && x.IsAbstract == false)
                            .Select(type => (ProtocolBase)Activator.CreateInstance(type)!).ToList();
                    }
                }

            // get instance form json string
            foreach (var serverBase in _baseList)
            {
                if (iDbServer.GetProtocol() == serverBase.Protocol
                    && iDbServer.GetClassVersion() == serverBase.ClassVersion)
                {
                    var jsonString = iDbServer.GetJson();
                    var ret = serverBase.CreateFromJsonString(jsonString);
                    if (ret == null) continue;
                    // set id.
                    ret.Id = iDbServer.GetId();

                    if (ret is ProtocolBaseWithAddressPortUserPwd p)
                    {
                        p.UsePrivateKeyForConnect ??= !string.IsNullOrEmpty(p.PrivateKey);
                        p.AskPasswordWhenConnect ??= string.IsNullOrEmpty(p.Password);
                    }
                    return ret;
                }
            }

            return null;
        }

        public static ProtocolBase? CreateFromJsonString(string jsonString)
        {
            var jObj = JsonConvert.DeserializeObject<dynamic>(jsonString);
            if (jObj == null ||
                jObj?.Protocol == null ||
                jObj?.ClassVersion == null)
                return null;

            // reflect all the child class
            lock (Locker)
            {
                if (_baseList.Count == 0)
                {
                    var assembly = typeof(ProtocolBase).Assembly;
                    var types = assembly.GetTypes();
                    _baseList = types.Where(item => item.IsSubclassOf(typeof(ProtocolBase)) && !item.IsAbstract)
                        .Select(type => (ProtocolBase)Activator.CreateInstance(type)!).ToList();
                }
            }

            // get instance form json string
            lock (Locker)
            {
                foreach (var @base in _baseList)
                {
                    if (jObj!.Protocol.ToString() == @base.Protocol &&
                        jObj.ClassVersion.ToString() == @base.ClassVersion)
                    {
                        var ret = @base.CreateFromJsonString(jsonString);
                        if (ret != null)
                        {
                            return ret;
                        }
                    }
                }
            }
            return null;
        }
    }
}