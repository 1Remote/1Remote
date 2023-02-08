using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Shawn.Utils;

/*
 * Script running:

powershell.exe $(SolutionDir)\scripts\Set-Secret.ps1 -fileDir .\Ui\Utils -fileName MsAppCenterHelper.cs -Pattern "===REPLACE_ME_WITH_APP_CENTER_SECRET===" -localSecretFilePath "C:\1Remote_Secret\AppCenterSecret.txt"
powershell.exe $(SolutionDir)\scripts\Set-Secret.ps1 -fileDir .\Ui\Utils -fileName MsAppCenterHelper.cs -Pattern "===REPLACE_ME_WITH_APP_CENTER_SECRET===" -localSecretFilePath "C:\1Remote_Secret\AppCenterSecret.txt" -isRevert

 */

namespace _1RM.Utils
{
    /// <summary>
    /// https://appcenter.ms/
    /// </summary>
    public static class MsAppCenterHelper
    {
        private static bool _isStarted = false;
        public static void Init(string secret)
        {
            AppCenter.LogLevel = LogLevel.Verbose;
            if (secret?.Length == "********-****-****-****-************".Length
                && "********-****-****-****-************".ToList()
                    .Select((c, i) => new { @char = c, index = i })
                    .Where(x => x.@char == '_')
                    .All(x => secret[x.index] == x.@char))
            {
                SimpleLogHelper.Debug(nameof(MsAppCenterHelper) + " init...");
                AppCenter.Start(secret, typeof(Analytics), typeof(Crashes));
                _isStarted = true;
            }
        }

        public static void Error(Exception e, IDictionary<string, string>? properties = null, params ErrorAttachmentLog[] attachments)
        {
            if (_isStarted == false) { return; }

            Crashes.TrackError(e, properties, attachments);
        }



        private static void Trace(EventName eventName, Dictionary<string, string> properties)
        {
            if (_isStarted == false) { return; }
#if DEBUG
            Analytics.TrackEvent(eventName.ToString() + "_Debug", properties);
#else
            Analytics.TrackEvent(eventName.ToString(), properties);
#endif
        }



        public static void TraceAppStatus(bool isStart, bool? isStoreVersion = null)
        {
            var properties = new Dictionary<string, string>
            {
                { "Action", isStart ? "Start":"Exit" },
            };
            if (isStart && isStoreVersion != null)
            {
                properties.Add("Version", isStoreVersion == true ? "MS Store" : "Exe");
            }
            Trace(EventName.App, properties);
        }

        public static void TraceView(string viewName, bool isShow)
        {
            var properties = new Dictionary<string, string>
            {
                { "View", viewName },
                { "Action", isShow ? "Show":"Hide" },
            };
            Trace(EventName.View, properties);
        }


        public static void TraceSessionOpen(string protocol, string via)
        {
            if (string.IsNullOrEmpty(via)) { return; }
            var properties = new Dictionary<string, string>
            {
                { "Action", "Start" },
                { "Protocol", protocol },
                { "Via", via },
            };
            Trace(EventName.SessionConnect, properties);
        }


        public static void TraceSessionEdit(string protocol)
        {
            if (_isStarted == false) { return; }
            var properties = new Dictionary<string, string>
            {
                { "Protocol", protocol },
            };
            Trace(EventName.SessionEdit, properties);
        }


        public static void TraceSpecial(string key, string value)
        {
            if (_isStarted == false) { return; }
            var properties = new Dictionary<string, string>
            {
                { key, value},
            };
            Trace(EventName.Special, properties);
        }
    }

    public enum EventName
    {
        App,
        View,
        SessionEdit,
        SessionConnect,
        Special,
    }
}
