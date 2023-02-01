using System;
using Newtonsoft.Json;
using _1RM.Model.Protocol.Base;
using Shawn.Utils;

namespace _1RM.Model.Protocol
{
    // ReSharper disable once InconsistentNaming
    public class VNC : ProtocolBaseWithAddressPortUserPwd
    {
        public enum EVncWindowResizeMode
        {
            Stretch = 0,
            Fixed = 1,
        }

        public static string ProtocolName = "VNC";
        public VNC() : base(ProtocolName, "VNC.V1", "VNC")
        {
            base.Port = "5900";
            base.UserName = "";
        }

        private EVncWindowResizeMode? _vncWindowResizeMode = EVncWindowResizeMode.Stretch;

        public EVncWindowResizeMode? VncWindowResizeMode
        {
            get => _vncWindowResizeMode;
            set => SetAndNotifyIfChanged(ref _vncWindowResizeMode, value);
        }

        public override bool IsOnlyOneInstance()
        {
            return true;
        }

        public override ProtocolBase? CreateFromJsonString(string jsonString)
        {
            try
            {
                return JsonConvert.DeserializeObject<VNC>(jsonString);
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