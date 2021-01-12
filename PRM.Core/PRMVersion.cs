using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PRM.Core
{
    public static class PRMVersion
    {
        public const int Major = 0;
        public const int Minor = 5;
        public const int Build = 8;
        public const int ReleaseDate = 2101112034;
        public static string Version => $"{Major}.{Minor}.{Build}.{ReleaseDate}";
    }
}
