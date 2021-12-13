using System.Text;

namespace PRM.Core
{
    public static class PRMVersion
    {
        public const int Major = 0;
        public const int Minor = 6;
        public const int Patch = 0;
        public const int Build = 0;
        public const string PreRelease = "beta.2"; // e.g. "alpha" "beta.2"
        public static string Version
        {
            get
            {
                var sb = new StringBuilder($"{Major}.{Minor}.{Patch}");
                if (Build > 0)
                    sb.Append($".{Build}");

                if (!string.IsNullOrEmpty(PreRelease))
                    sb.Append($"-{PreRelease}");

                return sb.ToString();
            }
        }
    }
}