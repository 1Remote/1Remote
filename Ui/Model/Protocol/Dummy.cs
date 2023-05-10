using System;
using Newtonsoft.Json;
using _1RM.Model.Protocol.Base;
using Shawn.Utils;

namespace _1RM.Model.Protocol
{
    public class Dummy : ProtocolBase
    {
        public static string ProtocolName = "";
        public Dummy() : base(ProtocolName, "", "")
        {
        }

        public override bool IsOnlyOneInstance()
        {
            return true;
        }

        public override ProtocolBase? CreateFromJsonString(string jsonString)
        {
            return null;
        }

        protected override string GetSubTitle()
        {
            return "";
        }

        public override double GetListOrder()
        {
            return -1;
        }
    }
}