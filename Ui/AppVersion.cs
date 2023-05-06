using static Shawn.Utils.VersionHelper;

namespace _1RM
{
    public static class AppVersion
    {
        public const uint Major = 1;
        public const uint Minor = 0;
        public const uint Patch = 0;
        public const uint Build = 0;
        public const string PreRelease = "beta.03"; // e.g. "alpha" "beta.2"


        public static readonly Version VersionData = new Version(Major, Minor, Patch, Build, PreRelease);
        public static string Version => VersionData.ToString();


        public static string[] UpdateCheckUrls =>
            string.IsNullOrEmpty(PreRelease)
                ? new[]
                {
                    "https://1remote.org/download/",
                    "https://github.com/1Remote/1Remote",
                }
                : new[]
                {
                    "https://github.com/1Remote/1Remote/releases/expanded_assets/Nightly",
                    "https://1remote.org/download/",
                    "https://github.com/1Remote/1Remote",
                };

        public static string[] UpdatePublishUrls =>
            string.IsNullOrEmpty(PreRelease)
                ? new[]
                {
                    "https://1remote.org/download/",
                    "https://github.com/1Remote/1Remote",
                }
                : new[]
                {
                    "https://github.com/1Remote/1Remote/releases/tag/Nightly",
                    "https://1remote.org/download/",
                    "https://github.com/1Remote/1Remote",
                };
    }
}