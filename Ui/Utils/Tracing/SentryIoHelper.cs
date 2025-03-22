using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using _1RM.Utils.WindowsApi;
using Sentry;
using Shawn.Utils;

namespace _1RM.Utils
{
    /// <summary>
    /// https://sentry.io/
    /// </summary>
    public static class SentryIoHelper
    {
        private static bool _hasInit = false;

        public static void Init(string dsn)
        {
            // disabled for OS under Win10
            if (WindowsVersionHelper.IsLowerThanWindows10())
                return;
            if (dsn?.Length > 0 && dsn != "===REPLACE_ME_WITH_SENTRY_IO_DEN===")
            {
                try
                {
                    SentrySdk.Init(options =>
                    {
                        options.Dsn = dsn; // Tells which project in Sentry to send events to: "https://22803c6274b266bbfb78e060f774883d@o4508311925686272.ingest.us.sentry.io/4508311950852096"
#if DEBUG
                        options.Debug = true;  // When configuring for the first time, to see what the SDK is doing:
#else
                        options.Debug = false;  // When configuring for the first time, to see what the SDK is doing:
#endif
                        options.TracesSampleRate = 1.0; // Set TracesSampleRate to 1.0 to capture 100% of transactions for tracing.
                        options.IsGlobalModeEnabled = true; // Enabling this option is recommended for client applications only. It ensures all threads use the same global scope.
                        options.AutoSessionTracking = true; // This option is recommended. It enables Sentry's "Release Health" feature.
                    });
                    _hasInit = true;
                    SimpleLogHelper.Debug(nameof(SentryIoHelper) + " init...");
                }
                catch (Exception e)
                {
                    SimpleLogHelper.Warning(e);
                }
            }
        }

        public static void Error(Exception e, IDictionary<string, string>? properties = null, Dictionary<string, string>? attachments = null)
        {
            if (_hasInit == false) { return; }
            properties ??= new Dictionary<string, string>();
            if(!properties.ContainsKey("Version")) properties.Add("Version", AppVersion.Version);
            if(!properties.ContainsKey("BuildDate")) properties.Add("Version", AppVersion.BuildDate);
#if FOR_MICROSOFT_STORE_ONLY
            if(!properties.ContainsKey("Distributor")) properties.Add("Distributor", $"{Assert.APP_NAME} MS Store");
#else
            properties.TryAdd("Distributor", $"{Assert.APP_NAME} Exe");
#endif

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

            SentrySdk.CaptureException(e, scope =>
            {
                foreach (var prop in properties)
                {
                    scope.SetTag(prop.Key, prop.Value);
                }

                if (attachments != null)
                {
                    foreach (var attachment in attachments)
                    {
                        scope.AddAttachment(Encoding.UTF8.GetBytes(attachment.Value), attachment.Key);
                    }
                }
            });
        }

        private static void Trace(EventName eventName, IDictionary<string, string>? properties = null)
        {
            if (_hasInit == false) { return; }
#if DEBUG
            string en = $"{eventName}_Debug";
#else
                        string en = eventName.ToString();
#endif
            SentrySdk.CaptureMessage(en, scope =>
            {
                if (properties == null) return;
                foreach (var prop in properties)
                {
                    scope.SetTag(prop.Key, prop.Value);
                }
            }, SentryLevel.Info);
        }

        //public static void TraceSessionOpen(string protocol, string via)
        //{
        //    if (string.IsNullOrEmpty(via)) { return; }
        //    var properties = new Dictionary<string, string>
        //    {
        //        { "Action", "Start" },
        //        { "Protocol", protocol },
        //        { "Via", via },
        //    };
        //    Trace(EventName.SessionConnect, properties);
        //}

        public static void TraceSpecial(Dictionary<string, string> kys)
        {
            if (_hasInit == false) { return; }
            Trace(EventName.Special, kys);
        }
    }
}