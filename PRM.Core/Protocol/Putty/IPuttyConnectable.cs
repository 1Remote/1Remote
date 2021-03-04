using PRM.Core.Model;
using System;
using Newtonsoft.Json;

namespace PRM.Core.Protocol.Putty
{
    public interface IPuttyConnectable
    {
        string GetPuttyConnString(PrmContext context);




        /// <summary>
        /// Allowing implementing interface only for specific class 'ProtocolServerBase'
        /// </summary>
        [JsonIgnore]
        ProtocolServerBase ProtocolServerBase { get; }

        string ExternalKittySessionConfigPath { get; set; }
    }

    public static class PuttyConnectableExtension
    {
        public static string GetSessionName(this IPuttyConnectable item)
        {
            if (item is ProtocolServerBase protocolServer)
            {
                return $"{SystemConfig.AppName}_{protocolServer.Protocol}_{protocolServer.Id}";
            }
            throw new NotSupportedException("you should not access here! something goes wrong");
        }
    }
}