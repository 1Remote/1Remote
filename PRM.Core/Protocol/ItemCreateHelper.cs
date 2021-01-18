using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using PRM.Core.Protocol;

namespace PRM.Core.DB
{
    public class ItemCreateHelper
    {
        private static readonly object Locker = new object();
        private static List<ProtocolServerBase> _baseList = new List<ProtocolServerBase>();
        public static ProtocolServerBase CreateFromDbOrm(Server item)
        {
            // reflect all the child class
            lock (Locker)
            {
                if (_baseList.Count == 0)
                {
                    var assembly = typeof(ProtocolServerBase).Assembly;
                    var types = assembly.GetTypes();
                    _baseList = types.Where(x => x.IsSubclassOf(typeof(ProtocolServerBase)) && !x.IsAbstract)
                        .Select(type => (ProtocolServerBase)Activator.CreateInstance(type)).ToList();
                }
                
                // get instance form json string
                foreach (var @base in _baseList)
                {
                    if (item.Protocol == @base.Protocol &&
                        item.ClassVersion == @base.ClassVersion)
                    {
                        var jsonStr = item.JsonConfigString;
                        //if (rsa != null)
                        //{
                        //    var tmp = rsa.DecodeOrNull(jsonStr);
                        //    if (tmp != null)
                        //    {
                        //        jsonStr = tmp;
                        //    }
                        //}
                        var ret = @base.CreateFromJsonString(jsonStr);
                        if (ret != null)
                        {
                            ret.Id = item.Id;
                            return ret;
                        }
                    }
                }
            }
            return null;
        }

        public static ProtocolServerBase CreateFromJsonString(string jsonString)
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
                    var assembly = typeof(ProtocolServerBase).Assembly;
                    var types = assembly.GetTypes();
                    _baseList = types.Where(item => item.IsSubclassOf(typeof(ProtocolServerBase)) && !item.IsAbstract)
                        .Select(type => (ProtocolServerBase)Activator.CreateInstance(type)).ToList();
                }
            }

            // get instance form json string
            lock (Locker)
            {
                foreach (var @base in _baseList)
                {
                    if (jObj.Protocol.ToString() == @base.Protocol &&
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
