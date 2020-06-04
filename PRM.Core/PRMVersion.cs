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
        public const int Minor = 3;
        public const int Build = 2;
        public const string ReleaseDate = "2006041921";
        public static string Version => $"{Major}.{Minor}.{Build}.{ReleaseDate}";
    }
}
