using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shawn.Utils;
using static Shawn.Utils.VersionHelper;

namespace Tests.Utils
{
    [TestClass()]
    public class VersionHelperTests
    {

        [TestMethod()]
        public void FromStringTest()
        {
            var v1 = new Version(0, 6, 1, 0);
            var v2 = Version.FromString(v1.ToString());
            Assert.IsTrue(v1 == v2);
        }

        [TestMethod()]
        public void CompareTest()
        {
            var v1 = new Version(0, 6, 1, 0);
            var v2 = new Version(0, 6, 1, 0);
            var v3 = new Version(0, 6, 1, 1);
            var v4 = new Version(0, 6, 2, 0);
            var v5 = new Version(0, 7, 1, 0);
            var v6 = new Version(1, 6, 1, 0);
            var v7 = new Version(0, 6, 1, 0, "alpha");
            var v8 = new Version(0, 6, 1, 0, "beta");
            var v9 = new Version(0, 6, 1, 0, "beta2");
            Assert.IsTrue(v1 == v2);
            Assert.IsTrue(v1 >= v2);
            Assert.IsTrue(v3 > v2);
            Assert.IsTrue(v3 != v2);
            Assert.IsTrue(v2 < v3);
            Assert.IsTrue(v3 >= v2);
            Assert.IsTrue(v4 > v3);
            Assert.IsTrue(v3 < v4);
            Assert.IsTrue(v3 <= v4);
            Assert.IsTrue(v5 > v4);
            Assert.IsTrue(v6 > v5);
            Assert.IsTrue(v6 > v7);
            Assert.IsTrue(v8 > v7);
            Assert.IsTrue(v9 > v8);
            Assert.IsTrue(v1 > v9);
            Assert.IsTrue(v9 != v8);
            Assert.IsTrue(Shawn.Utils.VersionHelper.Version.Compare(v1, v3) == true);
            Assert.IsTrue(Shawn.Utils.VersionHelper.Version.Compare(v9, v1) == true);
        }

        // Note: The former "VersionHelperTest" method was removed.
        // It referenced the old VersionHelper API (VersionHelper(Version) ctor, public
        // CheckUpdateFromUrl(url, ignoreVersion, content), 2-arg OnNewVersionRelease delegate,
        // CheckUpdateAsync(url, content)) which no longer exists. The current API fetches
        // content via HTTP internally, so content-injection testing is no longer possible;
        // the update-parsing logic is now covered only via VersionHelper.DefaultCheckMethod.
    }
}