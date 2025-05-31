using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace _1RM.Utils.Tracing
{
    internal static class UnifyTracing
    {
        public static void Init()
        {
            MsAppCenterHelper.Init(Assert.MS_APP_CENTER_SECRET);
            SentryIoHelper.Init(Assert.SENTRY_IO_DEN);
        }

        public static void Error(Exception e, IDictionary<string, string>? properties = null, Dictionary<string, string>? attachments = null)
        {
            MsAppCenterHelper.Error(e, properties, attachments);
            SentryIoHelper.Error(e, properties, attachments);
        }

        public static void TraceSpecial(Dictionary<string, string> kys)
        {
            MsAppCenterHelper.TraceSpecial(kys);
            SentryIoHelper.TraceSpecial(kys);
        }

        public static void TraceSessionOpen(string protocol, string via)
        {
            if (string.IsNullOrEmpty(via)) { return; }
            MsAppCenterHelper.TraceSessionOpen(protocol, via);
        }
    }
}
