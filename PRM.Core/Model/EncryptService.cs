using System.Diagnostics;
using com.github.xiangyuecn.rsacsharp;
using PRM.Core.Protocol;

namespace PRM.Core.Model
{
    public class EncryptService
    {
        private RSA _rsa { get; set; }



        public void EncryptInfo(ProtocolServerBase server)
        {
            if (_rsa == null) return;
            Debug.Assert(_rsa.DecodeOrNull(server.DisplayName) == null);
            server.DisplayName = _rsa.Encode(server.DisplayName);
            for (var i = 0; i < server.Tags.Count; i++)
            {
                server.Tags[i] = _rsa.Encode(server.Tags[i]);
            }

            if (server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortBase)))
            {
                var p = (ProtocolServerWithAddrPortBase)server;
                if (!string.IsNullOrEmpty(p.Address))
                    p.Address = _rsa.Encode(p.Address);
                p.Port = _rsa.Encode(p.Port);
            }
            if (server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase)))
            {
                var p = (ProtocolServerWithAddrPortUserPwdBase)server;
                if (!string.IsNullOrEmpty(p.UserName))
                    p.UserName = _rsa.Encode(p.UserName);
            }
        }
    }
}