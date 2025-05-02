using Newtonsoft.Json;
using _1RM.Model.Protocol.Base;

namespace _1RM.Utils.KiTTY
{
    public interface IKittyConnectable
    {
        /// <summary>
        /// Allowing implementing interface only for specific class 'ProtocolBase'
        /// </summary>
        [JsonIgnore]
        ProtocolBase ProtocolBase { get; }
        string ExternalKittySessionConfigPath { get; set; }
    }
}
