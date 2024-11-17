using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using _1RM.Utils.WindowsApi;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using MySqlX.XDevAPI.Common;
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
        private static bool _hasInit = false;
        public static void Init(string secret)
        {
            // disabled for OS under Win10
            if (WindowsVersionHelper.IsLowerThanWindows10())
                return;
            AppCenter.LogLevel = LogLevel.Verbose;
            if (secret?.Length == "********-****-****-****-************".Length
                && "********-****-****-****-************".ToList()
                    .Select((c, i) => new { @char = c, index = i })
                    .Where(x => x.@char == '_')
                    .All(x => secret[x.index] == x.@char))
            {
                SimpleLogHelper.Debug(nameof(MsAppCenterHelper) + " init...");
                AppCenter.Start(secret, typeof(Analytics), typeof(Crashes));
                _hasInit = true;
            }
        }

        public static void Error(Exception e, IDictionary<string, string>? properties = null, Dictionary<string, string>? attachments = null)
        {
            if (_hasInit == false) { return; }
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

            var list = new List<ErrorAttachmentLog>();
            foreach (var (k,v) in attachments ?? new Dictionary<string, string>())
            {
                list.Add(ErrorAttachmentLog.AttachmentWithText(v, k));
            }
            Crashes.TrackError(e, properties, list.ToArray());

            SentryIoHelper.Error(e, properties, attachments);
        }



        private static void Trace(EventName eventName, Dictionary<string, string> properties)
        {
            if (_hasInit == false) { return; }
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
            SentryIoHelper.TraceAppStatus(isStart, isStoreVersion);
        }

        public static void TraceView(string viewName, bool isShow)
        {
            var properties = new Dictionary<string, string>
            {
                { "View", (isShow ? "Show":"Hide") + viewName },
            };
            Trace(EventName.View, properties);
            SentryIoHelper.TraceView(viewName, isShow);
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
            SentryIoHelper.TraceSessionOpen(protocol, via);
        }


        public static void TraceSessionEdit(string protocol)
        {
            if (_hasInit == false) { return; }
            var properties = new Dictionary<string, string>
            {
                { "Protocol", protocol },
            };
            Trace(EventName.SessionEdit, properties);
            SentryIoHelper.TraceSessionEdit(protocol);
        }


        public static void TraceSpecial(Dictionary<string, string> kys)
        {
            if (_hasInit == false) { return; }
            Trace(EventName.Special, kys);
            SentryIoHelper.TraceSpecial(kys);
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
