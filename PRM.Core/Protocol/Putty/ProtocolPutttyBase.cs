using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PRM.Core.Annotations;
using PRM.Core.Model;

namespace PRM.Core.Protocol.Putty
{
    public abstract class ProtocolPutttyBase : ProtocolServerBase
    {
        public enum EProtocol
        {
            SSH,
        }
        public ProtocolPutttyBase(string serverType, string classVersion, string protocolDisplayName) : base(serverType, classVersion, protocolDisplayName)
        {
        }
        /// <summary>
        /// putty session name,auto generate
        /// </summary>
        public string SessionName => $"{SystemConfig.AppName}_{base.ServerType}_{base.Id}";
        public abstract string GetPuttyConnString();
    }
}
