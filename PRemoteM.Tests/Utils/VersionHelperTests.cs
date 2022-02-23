using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shawn.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Shawn.Utils.VersionHelper;

namespace TestsShawn.Utils
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
            Assert.IsTrue(Shawn.Utils.VersionHelper.Version.Compare(v9, v1) == false);
        }


        [TestMethod()]
        public void VersionHelperTest()
        {
            var v1 = new Version(0, 6, 1, 0);
            var v2 = new Version(0, 6, 2, 0);
            var v3 = new Version(0, 7, 1, 0);
            {
                var url = "www.xxxx.xx";
                var content = $"latest version: {v2.ToString()}";
                var checker = new VersionHelper(v1);
                var ret = checker.CheckUpdateFromUrl(url, null, content);
                Assert.IsTrue(ret.Item1);
                var v = Version.FromString(ret.Item2);
                Assert.IsTrue(v == v2);
                Assert.IsTrue(ret.Item3 == url);
            }
            {
                var url = "www.xxxx.xx";
                var content = $"latest version: {v2.ToString()}";
                var checker = new VersionHelper(v1);
                var ret = checker.CheckUpdateFromUrl(url, v3, content);
                Assert.IsTrue(ret.Item1 == false);
            }
            {
                var url = "www.xxxx.xx";
                var content = $"latest version: {v2.ToString()}";
                var checker = new VersionHelper(v1);
                var ret = checker.CheckUpdateFromUrl(url, v2, content);
                Assert.IsTrue(ret.Item1 == false);
            }
            {
                var url = "www.xxxx.xx";
                var content = $"latest version: {v3.ToString()}";
                var checker = new VersionHelper(v1);
                var ret = checker.CheckUpdateFromUrl(url, v2, content);
                Assert.IsTrue(ret.Item1 == true);
            }
            {
                var url = "www.xxxx.xx";
                var content = $"latest version: {v2.ToString()}";
                var checker = new VersionHelper(v1);
                var e = new ManualResetEvent(false);
                checker.OnNewVersionRelease += (version, url2) =>
                {
                    var v = Version.FromString(version);
                    Assert.IsTrue(url == url2);
                    Assert.IsTrue(v == v2);
                    e.Set();
                };
                checker.CheckUpdateAsync(url, content);
                if (e.WaitOne(3000) == false)
                {
                    Assert.Fail();
                }
            }
            {
                var url = "www.xxxx.xx";
                var content = $"latest version: {v2.ToString()}";
                var checker = new VersionHelper(v3);
                var e = new ManualResetEvent(false);
                checker.OnNewVersionRelease += (version, url2) =>
                {
                    e.Set();
                };
                checker.CheckUpdateAsync(url, content);
                if (e.WaitOne(3000) == true)
                {
                    Assert.Fail();
                }
            }
        }
    }
}