using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Shawn.Utils;

namespace PRM.Core.Protocol.VNC
{
    public class ProtocolServerVNC : ProtocolServerWithAddrPortUserPwdBase
    {
        public enum EVncWindowResizeMode
        {
            Stretch = 0,
            Fixed = 1,
        }

        public ProtocolServerVNC() : base("VNC", "VNC.V1", "VNC")
        {
            base.Port = "5900";
            base.UserName = "";
        }

        private EVncWindowResizeMode _vncWindowResizeMode = EVncWindowResizeMode.Stretch;

        public EVncWindowResizeMode VncWindowResizeMode
        {
            get => _vncWindowResizeMode;
            set => SetAndNotifyIfChanged(nameof(VncWindowResizeMode), ref _vncWindowResizeMode, value);
        }

        public override bool IsOnlyOneInstance()
        {
            return true;
        }

        public override ProtocolServerBase CreateFromJsonString(string jsonString)
        {
            try
            {
                return JsonConvert.DeserializeObject<ProtocolServerVNC>(jsonString);
            }
            catch (Exception e)
            {
                SimpleLogHelper.Debug(e);
                return null;
            }
        }

        public override double GetListOrder()
        {
            return 2;
        }
    }
}