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
        public const int Minor = 2;
        public const int Build = 5;
        public const string ReleaseDate = "2005161604";
        public static string Version => $"{Major}.{Minor}.{Build}.{ReleaseDate}";
    }
}
