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
            // https://stackoverflow.com/questions/2819934/detect-windows-version-in-net
            //+------------------------------------------------------------------------------+
            //|                    |   PlatformID    |   Major version   |      osBuild      |
            //+------------------------------------------------------------------------------+
            //| Windows 95         |  Win32Windows   |         4         |                   |
            //| Windows 98         |  Win32Windows   |         4         |                   |
            //| Windows Me         |  Win32Windows   |         4         |                   |
            //| Windows NT 4.0     |  Win32NT        |         4         |                   |
            //| Windows 2000       |  Win32NT        |         5         |                   |
            //| Windows XP         |  Win32NT        |         5         |                   |
            //| Windows 2003       |  Win32NT        |         5         |                   |
            //| Windows Vista      |  Win32NT        |         6         |                   |
            //| Windows 2008       |  Win32NT        |         6         |                   |
            //| Windows 7          |  Win32NT        |         6         |                   |
            //| Windows 2008 R2    |  Win32NT        |         6         |                   |
            //| Windows 8          |  Win32NT        |         6         |                   |
            //| Windows 8.1        |  Win32NT        |         6         |                   |
            //+------------------------------------------------------------------------------+
            //| Windows 10         |  Win32NT        |        10         |          0        |
            //| Windows 10 1909    |  Win32NT        |        10         |       18363       |
            //| Windows 10 2004    |  Win32NT        |        10         |       19041       |
            //| Windows 10 20H2    |  Win32NT        |        10         |       19042       |
            //| Windows 10 21H2    |  Win32NT        |        10         |       19043       |
            //| Windows 11         |  Win32NT        |        10         |       22000       |
            //+------------------------------------------------------------------------------+
            // disabled for OS under Win10
            if (Environment.OSVersion.Version.Build < 18363)
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
                _isStarted = true;
            }
#endif
        }

        public static void Error(Exception e, IDictionary<string, string>? properties = null, List<ErrorAttachmentLog>? attachments = null)
        {
#if DEBUG
            return;
#else
            if (_isStarted == false) { return; }
            properties ??= new Dictionary<string, string>();
            if (!properties.ContainsKey("Version"))
                properties.Add("Version", AppVersion.Version);
            if (attachments != null)
                Crashes.TrackError(e, properties, attachments.ToArray());
            else
                Crashes.TrackError(e, properties);
#endif
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
