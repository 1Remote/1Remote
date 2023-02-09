using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.View;

namespace _1RM.Model
{
    public static class GlobalEventHelper
    {
        public delegate void OnRequestQuickConnectDelegate(ProtocolBase server, string assignTabToken = "", string assignRunnerName = "", string fromView = "", string assignCredentialName = "");
        public static OnRequestQuickConnectDelegate? OnRequestQuickConnect { get; set; } = null;


        public delegate void OnRequestServerConnectDelegate(string serverId, string assignTabToken = "", string assignRunnerName = "", string fromView = "", string assignCredentialName = "");
        /// <summary>
        /// Invoke notify to open a new remote session to Tab with assignTabToken (if assignTabToken != null).
        /// </summary>
        public static OnRequestServerConnectDelegate? OnRequestServerConnect { get; set; } = null;


        /// <summary>
        /// Go to server edit by server id, if id == 0 goto add page
        /// </summary>
        /// <param name="presetTagNames">preset tag names</param>
        /// <param name="showAnimation">show in animation?</param>
        public delegate void OnGoToServerAddPageDelegate(List<string>? presetTagNames = null, bool showAnimation = true);
        public static OnGoToServerAddPageDelegate? OnGoToServerAddPage { get; set; } = null;

        public delegate void OnRequestGoToServerDuplicatePageDelegate(ProtocolBase server, bool showAnimation = true);
        public static OnRequestGoToServerDuplicatePageDelegate? OnRequestGoToServerDuplicatePage { get; set; } = null;


        /// <summary>
        /// Go to server edit or duplicate
        /// </summary>
        /// <param name="showAnimation">show in animation?</param>
        public delegate void OnRequestGoToServerEditPageDelegate(ProtocolBase server, bool showAnimation = true);

        /// <summary>
        /// Go to server edit by server id
        /// param1 int: server id
        /// param2 bool: is duplicate?
        /// param3 bool: show in animation?
        /// </summary>
        public static OnRequestGoToServerEditPageDelegate? OnRequestGoToServerEditPage { get; set; } = null;

        public delegate void OnRequestGoToServerMultipleEditPageDelegate(IEnumerable<ProtocolBase> servers, bool showAnimation = true);
        public static OnRequestGoToServerMultipleEditPageDelegate? OnRequestGoToServerMultipleEditPage { get; set; } = null;

        public delegate void OnRequestDeleteServerDelegate(ProtocolBase server);
        public static OnRequestDeleteServerDelegate? OnRequestDeleteServer { get; set; } = null;


        /// <summary>
        /// Invoke to notify language was changed.
        /// </summary>
        public static Action? OnLanguageChanged { get; set; } = null;

        /// <summary>
        /// OnScreenResolutionChanged
        /// </summary>
        public static Action? OnScreenResolutionChanged { get; set; } = null;

        public delegate void OnFilterChangedDelegate(string filterString = "");
    }
}
