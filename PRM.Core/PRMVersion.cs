using System.Text;
using Shawn.Utils;

namespace PRM.Core
{
    public static class PRMVersion
    {
        public const uint Major = 0;
        public const uint Minor = 6;
        public const uint Patch = 2;
        public const uint Build = 2;
        public const string PreRelease = ""; // e.g. "alpha" "beta.2"

        public static readonly VersionHelper.Version VersionData = new VersionHelper.Version(Major, Minor, Patch, Build, PreRelease);
        public static string Version => VersionData.ToString();


        public static readonly string[] UpdateUrls =
        {
            "https://github.com/VShawn/PRemoteM",
            "https://gitee.com/vshawn/PRemoteM/blob/dev/readme.md",
#if DEV
            "https://github.com/VShawn/PRemoteM-Test/wiki",
#endif
        };
    }
}