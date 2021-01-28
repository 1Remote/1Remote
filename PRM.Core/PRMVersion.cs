namespace PRM.Core
{
    public static class PRMVersion
    {
        public const int Major = 0;
        public const int Minor = 5;
        public const int Build = 8;
        public const int ReleaseDate = 2101282043;
        public static string Version => $"{Major}.{Minor}.{Build}.{ReleaseDate}";
    }
}
