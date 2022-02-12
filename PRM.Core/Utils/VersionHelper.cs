using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PRM.Core.Model;

namespace Shawn.Utils
{
    public class VersionHelper
    {
        public class Version
        {
            protected bool Equals(Version? other)
            {
                if (other is null)
                    return false;
                return Major == other.Major && Minor == other.Minor && Patch == other.Patch && Build == other.Build && PreRelease == other.PreRelease;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((Version)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = (int)Major;
                    hashCode = (hashCode * 397) ^ (int)Minor;
                    hashCode = (hashCode * 397) ^ (int)Patch;
                    hashCode = (hashCode * 397) ^ (int)Build;
                    hashCode = (hashCode * 397) ^ (PreRelease != null ? PreRelease.GetHashCode() : 0);
                    return hashCode;
                }
            }

            public readonly uint Major;
            public readonly uint Minor;
            public readonly uint Patch;
            public readonly uint Build;
            public readonly string PreRelease;

            public Version(uint major, uint minor, uint patch, uint build, string preRelease = "")
            {
                this.Major = major;
                this.Minor = minor;
                this.Patch = patch;
                this.Build = build;
                this.PreRelease = preRelease;
            }

            public override string ToString()
            {
                var sb = new StringBuilder($"{Major}.{Minor}.{Patch}");
                if (Build > 0)
                    sb.Append($".{Build}");

                if (!string.IsNullOrEmpty(PreRelease))
                    sb.Append($"-{PreRelease}");

                return sb.ToString();
            }

            public static Version FromString(string versionString)
            {
                bool isPreRelease = versionString.IndexOf("-", StringComparison.Ordinal) > 0;
                var splits = versionString?.Split(new[] { ".", "-" }, StringSplitOptions.RemoveEmptyEntries);
                uint major = 0;
                uint minor = 0;
                uint patch = 0;
                uint build = 0;
                string preRelease = "";
                if (splits?.Length >= 3)
                {
                    if (uint.TryParse(splits[0], out var majorTmp)
                        && uint.TryParse(splits[1], out var minorTmp)
                        && uint.TryParse(splits[2], out var patchTmp)
                    )
                    {
                        major = majorTmp;
                        minor = minorTmp;
                        patch = patchTmp;
                    }
                }

                if (splits?.Length >= 4)
                {
                    if (uint.TryParse(splits[3], out var tmp))
                    {
                        if (splits?.Length == 5)
                        {
                            build = tmp;
                            preRelease = splits[4];
                        }
                        else if (isPreRelease == false)
                        {
                            build = tmp;
                        }
                        else
                        {
                            preRelease = splits[3];
                        }
                    }
                    else
                    {
                        preRelease = splits[3];
                    }
                }
                return new Version(major, minor, patch, build, preRelease);
            }

            public static bool operator >(Version a, Version b)
            {
                if (a == b)
                    return false;
                return Compare(b, a);
            }

            public static bool operator <(Version a, Version b)
            {
                if (a == b)
                    return false;
                return Compare(a, b);
            }

            public static bool operator >=(Version a, Version b)
            {
                return a.Equals(b) || Compare(b, a);
            }

            public static bool operator <=(Version a, Version b)
            {
                return a.Equals(b) || Compare(a, b);
            }

            public static bool operator ==(Version a, Version b)
            {
                if (a is null && b is null)
                    return true;
                return a.Equals(b);
            }

            public static bool operator !=(Version a, Version b)
            {
                if (a is null && b is null)
                    return false;
                return !a.Equals(b);
            }

            /// <summary>
            /// if v2 is newer, return true
            /// </summary>
            /// <param name="v1"></param>
            /// <param name="v2"></param>
            /// <returns></returns>
            public static bool Compare(Version v1, Version v2)
            {
                if (v1 == null || v2 == null)
                    return false;

                if (v2.Major > v1.Major)
                    return true;
                if (v2.Major == v1.Major
                    && v2.Minor > v1.Minor)
                    return true;
                if (v2.Major == v1.Major
                    && v2.Minor == v1.Minor
                    && v2.Patch > v1.Patch)
                    return true;
                if (v2.Major == v1.Major
                    && v2.Minor == v1.Minor
                    && v2.Patch == v1.Patch
                    && v2.Build > v1.Build)
                    return true;

                if (v2.Major == v1.Major
                    && v2.Minor == v1.Minor
                    && v2.Patch == v1.Patch
                    && v2.Build == v1.Build
                    && string.CompareOrdinal(v2.PreRelease.ToLower(), v1.PreRelease.ToLower()) > 0)
                    return true;

                return false;
            }
        }



        /// <summary>
        /// Invoke to notify a newer version of te software was released
        /// while new version code = arg1, download url = arg2
        /// </summary>
        public delegate void OnNewVersionReleaseDelegate(string version, string url);

        private readonly string[] _urls;

        /// <summary>
        /// Invoke to notify a newer version of te software was released
        /// while new version code = arg1, download url = arg2
        /// </summary>
        public OnNewVersionReleaseDelegate OnNewVersionRelease = null;

        private readonly Version _currentVersion;
        public Version IgnoreVersion;

        public VersionHelper(Version version, Version ignoreVersion = null, string[] urls = null)
        {
            _currentVersion = version;
            IgnoreVersion = ignoreVersion;
            _urls = urls;
        }



        public Tuple<bool, string, string> CheckUpdateFromUrl(string url, Version ignoreVersion = null, string urlContent = "")
        {
            try
            {
                string html = "";
                if (string.IsNullOrEmpty(urlContent) == false)
                    html = urlContent;
                else
                    html = HttpHelper.Get(url).ToLower();

                var vs = Regex.Match(html, @"latest\sversion:\s*([\d|.]*)");
                if (vs.Success)
                {
                    var tmp = vs.ToString();
                    var versionString = tmp.Substring(tmp.IndexOf("version:", StringComparison.OrdinalIgnoreCase) + "version:".Length + 1).Trim();
                    var releasedVersion = Version.FromString(versionString);
                    if (ignoreVersion != null)
                    {
                        if (releasedVersion <= ignoreVersion)
                        {
                            return new Tuple<bool, string, string>(false, "", url);
                        }
                    }
                    if (releasedVersion > _currentVersion)
                        return new Tuple<bool, string, string>(true, versionString, url);
                }
            }
            catch (Exception e)
            {
                SimpleLogHelper.Warning(e);
            }
            return new Tuple<bool, string, string>(false, "", url);
        }



        /// <summary>
        /// Check if new release, return true + url.
        /// </summary>
        /// <returns></returns>
        public Tuple<bool, string, string> CheckUpdate(string assignUrl = "", string assignUrlContent = "")
        {
            if (_urls?.Length > 0)
                foreach (var url in _urls)
                {
                    var tuple = CheckUpdateFromUrl(url);
                    if (tuple.Item1)
                        return tuple;
                }

            if (string.IsNullOrEmpty(assignUrlContent) == false)
            {
                return CheckUpdateFromUrl(assignUrl, null, assignUrlContent);
            }

            return new Tuple<bool, string, string>(false, "", "");
        }

        /// <summary>
        /// Check if new release, invoke OnNewRelease with new version & url.
        /// </summary>
        /// <returns></returns>
        public void CheckUpdateAsync(string assignUrl = "", string assignUrlContent = "")
        {
            var t = new Task(() =>
            {
                var r = CheckUpdate(assignUrl, assignUrlContent);
                if (r.Item1)
                {
                    OnNewVersionRelease?.Invoke(r.Item2, r.Item3);
                }
            });
            t.Start();
        }
    }
}