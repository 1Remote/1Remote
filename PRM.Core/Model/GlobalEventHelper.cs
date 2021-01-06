using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PRM.Core.Model
{
    public static class GlobalEventHelper
    {
        public delegate void OnServerConnectDelegate(uint serverId, string assignTabToken = null);

        /// <summary>
        /// Invoke notify to open a new remote session to Tab with assignTabToken (if assignTabToken != null).
        /// </summary>
        public static OnServerConnectDelegate OnRequireServerConnect { get; set; } = null;


        /// <summary>
        /// Go to server edit by server id, if id == 0 goto add page
        /// </summary>
        /// <param name="presetGroupName">preset group name</param>
        /// <param name="showAnimation">show in animation?</param>
        public delegate void OnGoToServerAddPageDelegate(string presetGroupName = "", bool showAnimation = true);

        /// <summary>
        /// Go to server edit by server id, if id == 0 goto add page
        /// </summary>
        /// <param name="serverId">server id, if id == 0 goto add page</param>
        /// <param name="isDuplicateMode">is duplicate? works only id above 0</param>
        /// <param name="showAnimation">show in animation?</param>
        public delegate void OnGoToServerEditPageDelegate(uint serverId, bool isDuplicateMode = false, bool showAnimation = true);

        /// <summary>
        /// Go to server add
        /// param3 bool: show in animation?
        /// </summary>
        public static OnGoToServerAddPageDelegate OnGoToServerAddPage { get; set; } = null;


        /// <summary>
        /// Go to server edit by server id
        /// param1 uint: server id
        /// param2 bool: is duplicate?
        /// param3 bool: show in animation?
        /// </summary>
        public static OnGoToServerEditPageDelegate OnGoToServerEditPage { get; set; } = null;

        /// <summary>
        /// Invoke to notify a newer version of te software was released
        /// while new version code = arg1, download url = arg2
        /// </summary>
        public static Action<string, string> OnNewVersionRelease { get; set; } = null;


        /// <summary>
        /// Invoke to show up progress bar when arg2 > 0
        /// while progress percent = arg1 / arg2 * 100%, alert info = arg3
        /// </summary>
        public static Action<int, int, string> OnLongTimeProgress { get; set; } = null;

        /// <summary>
        /// Invoke to notify language was changed.
        /// </summary>
        public static Action OnLanguageChanged { get; set; } = null;



        /// <summary>
        /// OnScreenResolutionChanged
        /// </summary>
        public static Action OnScreenResolutionChanged { get; set; } = null;
    }
}
