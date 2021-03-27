using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PRM.Core.Model;

namespace Shawn.Utils
{
    public class UpdateChecker
    {
        /// <summary>
        /// Invoke to notify a newer version of te software was released
        /// while new version code = arg1, download url = arg2
        /// </summary>
        public delegate void OnNewVersionReleaseDelegate(string version, string url);

        private readonly string[] _urls =
        {
            "https://github.com/VShawn/PRemoteM",
#if DEV
            "https://github.com/VShawn/PRemoteM-Test/wiki",
#endif
        };

        /// <summary>
        /// Invoke to notify a newer version of te software was released
        /// while new version code = arg1, download url = arg2
        /// </summary>
        public OnNewVersionReleaseDelegate OnNewVersionRelease = null;

        private readonly string _currentVersion;

        public UpdateChecker(string currentVersion)
        {
            _currentVersion = currentVersion;
        }

        /// <summary>
        /// if newer, return true
        /// </summary>
        /// <param name="versionString"></param>
        /// <returns></returns>
        private bool Compare(string versionString)
        {
            return Compare(_currentVersion, versionString);
        }

        /// <summary>
        /// if versionString2 is newer, return true
        /// </summary>
        /// <param name="versionString1"></param>
        /// <param name="versionString2"></param>
        /// <returns></returns>
        private bool Compare(string versionString1, string versionString2)
        {
            if (string.IsNullOrEmpty(versionString2))
                return false;
            if (string.IsNullOrEmpty(versionString1))
                return true;

            var x1 = FromVersionString(versionString1);
            var x2 = FromVersionString(versionString2);
            if (string.IsNullOrWhiteSpace(x1.Item5) && !string.IsNullOrWhiteSpace(x2.Item5))
                return false;

            if (x2.Item1 > x1.Item1)
                return true;
            if (x2.Item1 == x1.Item1
                && x2.Item2 > x1.Item2)
                return true;
            if (x2.Item1 == x1.Item1
                && x2.Item2 == x1.Item2
                && x2.Item3 > x1.Item3)
                return true;
            if (x2.Item1 == x1.Item1
                && x2.Item2 == x1.Item2
                && x2.Item3 == x1.Item3
                && x2.Item4 > x1.Item4)
                return true;
            return false;
        }

        private Tuple<int, int, int, int, string> FromVersionString(string versionString)
        {
            bool isPreRelease = versionString.IndexOf("-", StringComparison.Ordinal) > 0;
            var splits = versionString?.Split(new[] { ".", "-" }, StringSplitOptions.RemoveEmptyEntries);
            int major = -1;
            int minor = -1;
            int patch = -1;
            int build = 0;
            string preRelease = "";
            if (splits?.Length >= 3)
            {
                if (int.TryParse(splits[0], out var majorTmp)
                    && int.TryParse(splits[1], out var minorTmp)
                    && int.TryParse(splits[2], out var patchTmp)
                )
                {
                    major = majorTmp;
                    minor = minorTmp;
                    patch = patchTmp;
                }
            }

            if (splits?.Length >= 4)
            {
                if (int.TryParse(splits[3], out var tmp))
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


            return new Tuple<int, int, int, int, string>(major, minor, patch, build, preRelease);
        }

        public Tuple<bool, string, string> CheckUpdate(string url, string ignoreVersion = "")
        {
            try
            {
                var html = HttpHelper.Get(url).ToLower();
                var vs = Regex.Match(html, @"latest\sversion:\s*([\d|.]*)");
                if (vs.Success)
                {
                    var tmp = vs.ToString();
                    var version = tmp.Substring(tmp.IndexOf("version:") + "version:".Length + 1).Trim();
                    if (Compare(ignoreVersion, version))
                        if (Compare(version))
                        {
                            return new Tuple<bool, string, string>(true, version, url);
                        }
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
        public Tuple<bool, string, string> CheckUpdate()
        {
            foreach (var url in _urls)
            {
                var tuple = CheckUpdate(url);
                if (tuple.Item1)
                    return tuple;
            }
            return new Tuple<bool, string, string>(false, "", "");
        }

        /// <summary>
        /// Check if new release, invoke OnNewRelease with new version & url.
        /// </summary>
        /// <returns></returns>
        public void CheckUpdateAsync()
        {
            var t = new Task(() =>
            {
                var r = CheckUpdate();
                if (r.Item1)
                {
                    OnNewVersionRelease?.Invoke(r.Item2, r.Item3);
                }
            });
            t.Start();
        }
    }
}