using _1RM.Model.Protocol.Base;
using Newtonsoft.Json;

namespace _1RM.Utils.PuTTY
{
    public interface IPuttyConnectable
    {
        /// <summary>
        /// Allowing implementing interface only for specific class 'ProtocolBase'
        /// </summary>
        [JsonIgnore]
        ProtocolBase ProtocolBase { get; }
        // TODO: delete after 2026-01-01
        string ExternalKittySessionConfigPath { get; set; }
        string ExternalSessionConfigPath { get; set; }

    }
}
