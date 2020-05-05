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
        public const int Build = 1;
        public const string ReleaseDate = "202005032234";
        public static string Version => $"{Major}.{Minor}.{Build}.{ReleaseDate}";
    }
}
