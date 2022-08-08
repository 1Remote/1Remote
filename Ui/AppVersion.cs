using Shawn.Utils;

namespace PRM
{
    public static class AppVersion
    {
        public const uint Major = 0;
        public const uint Minor = 7;
        public const uint Patch = 1;
        public const uint Build = 4;
        public const string PreRelease = ""; // e.g. "alpha" "beta.2"

        public static readonly VersionHelper.Version VersionData = new VersionHelper.Version(Major, Minor, Patch, Build, PreRelease);
        public static string Version => VersionData.ToString();


        public static readonly string[] UpdateUrls =
        {
            "https://github.com/1Remote/PRemoteM",
            "https://github.com/VShawn/PRemoteM",
            "https://gitee.com/vshawn/PRemoteM"
        };
    }
}