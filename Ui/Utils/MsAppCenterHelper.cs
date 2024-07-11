using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using _1RM.Utils.WindowsApi;
using Google.Protobuf.WellKnownTypes;
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
        private static bool _isInited = false;
        public static void Init(string secret)
        {
            // disabled for OS under Win10
            if (WindowsVersionHelper.IsLowerThanWindows10())
                return;
#if !DEBUG
            AppCenter.LogLevel = LogLevel.Verbose;
            if (secret?.Length == "********-****-****-****-************".Length
                && "********-****-****-****-************".ToList()
                    .Select((c, i) => new { @char = c, index = i })
                    .Where(x => x.@char == '_')
                    .All(x => secret[x.index] == x.@char))
            {
                SimpleLogHelper.Debug(nameof(MsAppCenterHelper) + " init...");
                AppCenter.Start(secret, typeof(Analytics), typeof(Crashes));
                _isInited = true;
            }
#endif
        }

        public static void Error(Exception e, IDictionary<string, string>? properties = null, List<ErrorAttachmentLog>? attachments = null)
        {
#if DEBUG
            return;
#else
            if (_isInited == false) { return; }
            properties ??= new Dictionary<string, string>();
            if (!properties.ContainsKey("Version"))
                properties.Add("Version", AppVersion.Version);
            if (!properties.ContainsKey("BuildDate"))
                properties.Add("BuildDate", AppVersion.BuildDate);

            if (properties.ContainsKey("StackTrace") == false)
                try
                {
                    string message = "";
                    var stacktrace = new StackTrace();
                    for (var i = 0; i < stacktrace.FrameCount; i++)
                    {
                        var frame = stacktrace.GetFrame(i);
                        if (frame == null) continue;
                        message += frame.GetMethod() + " -> " + frame.GetFileName() + ": " + frame.GetFileLineNumber() + "\r\n";
                    }
                    properties.Add("StackTrace", message);
                }
                catch
                {
                    // ignore
                }

            if (attachments != null)
                Crashes.TrackError(e, properties, attachments.ToArray());
            else
                Crashes.TrackError(e, properties);
#endif
        }



        private static void Trace(EventName eventName, Dictionary<string, string> properties)
        {
            if (_isInited == false) { return; }
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
                { "View", (isShow ? "Show":"Hide") + viewName },
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
            if (_isInited == false) { return; }
            var properties = new Dictionary<string, string>
            {
                { "Protocol", protocol },
            };
            Trace(EventName.SessionEdit, properties);
        }


        public static void TraceSpecial(Dictionary<string, string> kys)
        {
            if (_isInited == false) { return; }
            Trace(EventName.Special, kys);
        }
        public static void TraceSpecial(string key, string value)
        {
            var properties = new Dictionary<string, string>
            {
                { key, value},
            };
            TraceSpecial(properties);
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
