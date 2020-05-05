using PRM.Core.Model;
using System;
using Newtonsoft.Json;

namespace PRM.Core.Protocol.Putty
{
    public interface IPuttyConnectable
    {
        string GetPuttyConnString();
        /// <summary>
        /// Allowing implementing interface only for specific class 'ProtocolServerBase'
        /// </summary>
        [JsonIgnore]
        ProtocolServerBase ProtocolServerBase { get; }
    }

    public static class PuttyConnectableExtension
    {
        public static string GetSessionName(this IPuttyConnectable item)
        {
            if (item is ProtocolServerBase protocolServer)
            {
                return $"{SystemConfig.AppName}_{protocolServer.ServerType}_{protocolServer.Id}";
            }
            throw new NotSupportedException("you should not access here! something goes wrong");
        }
    }
}