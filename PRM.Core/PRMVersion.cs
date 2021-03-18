namespace PRM.Core
{
    public static class PRMVersion
    {
        public const int Major = 0;
        public const int Minor = 5;
        public const int Patch = 10;
        public const string PreRelease = ""; // e.g. "alpha" "beta.2"
        public static string Version => string.IsNullOrWhiteSpace(PreRelease) ? $"{Major}.{Minor}.{Patch}" : $"{Major}.{Minor}.{Patch}-{PreRelease}";
    }
}