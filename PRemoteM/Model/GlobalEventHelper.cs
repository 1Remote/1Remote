using System;
using System.Collections.Generic;
using System.Windows;
using PRM.Model.Protocol.Base;

namespace PRM.Model
{
    public static class GlobalEventHelper
    {
        public delegate void OnServerConnectDelegate(long serverId, string assignTabToken = null, string assignRunnerName = null);

        /// <summary>
        /// Invoke notify to open a new remote session to Tab with assignTabToken (if assignTabToken != null).
        /// </summary>
        public static OnServerConnectDelegate OnRequestServerConnect { get; set; } = null;


        /// <summary>
        /// Go to server edit by server id, if id == 0 goto add page
        /// </summary>
        /// <param name="presetTagNames">preset tag names</param>
        /// <param name="showAnimation">show in animation?</param>
        public delegate void OnGoToServerAddPageDelegate(List<string> presetTagNames = null, bool showAnimation = true);

        public static OnGoToServerAddPageDelegate OnGoToServerAddPage { get; set; } = null;



        /// <summary>
        /// Go to server edit by server id, if id == 0 goto add page
        /// </summary>
        /// <param name="serverId">server id, if id == 0 goto add page</param>
        /// <param name="isDuplicateMode">is duplicate? works only id above 0</param>
        /// <param name="showAnimation">show in animation?</param>
        public delegate void OnRequestGoToServerEditPageDelegate(int serverId, bool isDuplicateMode = false, bool showAnimation = true);
        /// <summary>
        /// Go to server edit by server id
        /// param1 int: server id
        /// param2 bool: is duplicate?
        /// param3 bool: show in animation?
        /// </summary>
        public static OnRequestGoToServerEditPageDelegate OnRequestGoToServerEditPage { get; set; } = null;

        public delegate void OnRequestGoToServerMultipleEditPageDelegate(IEnumerable<ProtocolServerBase> servers, bool showAnimation = true);
        public static OnRequestGoToServerMultipleEditPageDelegate OnRequestGoToServerMultipleEditPage { get; set; } = null;

        public delegate void OnRequestDeleteServerDelegate(int serverId);
        public static OnRequestDeleteServerDelegate OnRequestDeleteServer { get; set; } = null;

        /// <summary>
        /// Invoke to show up processing ring
        /// alert info = arg2
        /// </summary>
        public static Action<Visibility, string> ShowProcessingRing { get; set; } = null;

        /// <summary>
        /// Invoke to notify language was changed.
        /// </summary>
        public static Action OnLanguageChanged { get; set; } = null;

        /// <summary>
        /// OnScreenResolutionChanged
        /// </summary>
        public static Action OnScreenResolutionChanged { get; set; } = null;

        public static Action OnLauncherHotKeyChanged { get; set; }
    }
}
