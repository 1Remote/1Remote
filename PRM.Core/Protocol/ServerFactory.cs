using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using PRM.Core.Model;
using PRM.Core.Protocol;

namespace PRM.Core.DB
{
    public class ServerFactory
    {
        #region 单例
        private static ServerFactory instance;
        private static readonly object InstanceLock = new object();
        private ServerFactory()
        {
        }
        public static ServerFactory GetInstance()
        {
            lock (InstanceLock)
            {
                if (instance == null)
                {
                    instance = new ServerFactory();
                }
            }
            return instance;
        }
        #endregion

        private static readonly object Locker = new object();
        private List<ProtocolServerBase> _baseList = new List<ProtocolServerBase>();
        public ProtocolServerBase CreateFromDbObjectServerOrm(Server server)
        {
            // reflect all the child class
            lock (Locker)
            {
                if (_baseList.Count == 0)
                {
                    var assembly = typeof(ProtocolServerBase).Assembly;
                    var types = assembly.GetTypes();
                    _baseList = types.Where(item => item.IsSubclassOf(typeof(ProtocolServerBase)) && !item.IsAbstract)
                        .Select(type => (ProtocolServerBase)Activator.CreateInstance(type)).ToList();
                }
                
                // get instance form json string
                foreach (var serverAbstract in _baseList)
                {
                    if (server.Protocol == serverAbstract.Protocol &&
                        server.ClassVersion == serverAbstract.ClassVersion)
                    {
                        var jsonStr = server.JsonConfigString;
                        //if (rsa != null)
                        //{
                        //    var tmp = rsa.DecodeOrNull(jsonStr);
                        //    if (tmp != null)
                        //    {
                        //        jsonStr = tmp;
                        //    }
                        //}
                        var ret = serverAbstract.CreateFromJsonString(jsonStr);
                        if (ret != null)
                        {
                            ret.Id = server.Id;
                            return ret;
                        }
                    }
                }
            }
            return null;
        }

        public ProtocolServerBase CreateFromJsonString(string jsonString)
        {
            var jObj = JsonConvert.DeserializeObject<dynamic>(jsonString);
            if (jObj == null ||
                jObj.Protocol == null ||
                jObj.ClassVersion == null)
                return null;

            // reflect all the child class
            lock (Locker)
            {
                if (_baseList.Count == 0)
                {
                    var assembly = typeof(ProtocolServerBase).Assembly;
                    var types = assembly.GetTypes();
                    _baseList = types.Where(item => item.IsSubclassOf(typeof(ProtocolServerBase)) && !item.IsAbstract)
                        .Select(type => (ProtocolServerBase)Activator.CreateInstance(type)).ToList();
                }
            }

            // get instance form json string
            foreach (var serverAbstract in _baseList)
            {
                if (jObj.Protocol.ToString() == serverAbstract.Protocol &&
                    jObj.ClassVersion.ToString() == serverAbstract.ClassVersion)
                {
                    var ret = serverAbstract.CreateFromJsonString(jsonString);
                    if (ret != null)
                    {
                        return ret;
                    }
                }
            }
            return null;
        }
    }
}
