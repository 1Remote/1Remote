using Shawn.Utils;

namespace _1RM
{
    public static class AppVersion
    {
        public const uint Major = 0;
        public const uint Minor = 9;
        public const uint Patch = 0;
        public const uint Build = 0;
        public const string PreRelease = "alpha.01"; // e.g. "alpha" "beta.2"


        public static readonly VersionHelper.Version VersionData = new VersionHelper.Version(Major, Minor, Patch, Build, PreRelease);
        public static string Version => VersionData.ToString();


        public static readonly string[] UpdateUrls =
        {
            "https://github.com/1Remote/1Remote",
            "https://github.com/1Remote/PRemoteM",
        };
    }
}