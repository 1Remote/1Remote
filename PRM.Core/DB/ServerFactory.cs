using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using PRM.Core.Base;

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
        List<ServerAbstract> _baseList = new List<ServerAbstract>();
        public ServerAbstract CreateFromDb(ServerOrm serverOrm)
        {
            // reflect all the child class
            lock (Locker)
            {
                if (_baseList.Count == 0)
                {
                    _baseList = Assembly.GetExecutingAssembly().GetTypes()
                        .Where(item => item.IsSubclassOf(typeof(ServerAbstract)))
                        .Select(type => (ServerAbstract)Activator.CreateInstance(type)).ToList();
                }
            }

            // get instance form json string
            foreach (var serverAbstract in _baseList)
            {
                if (serverOrm.ServerType == serverAbstract.ServerType &&
                    serverOrm.ClassVersion == serverAbstract.ClassVersion)
                {
                    var ret = serverAbstract.CreateFromJsonString(serverOrm.JsonConfigString);
                    if (ret != null)
                    {
                        ret.Id = serverOrm.Id;
                        return ret;
                    }
                }
            }
            return null;
        }
    }
}
