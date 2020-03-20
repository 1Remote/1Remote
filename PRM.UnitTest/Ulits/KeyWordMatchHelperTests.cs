using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shawn.Ulits;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shawn.Ulits.Tests
{
    [TestClass()]
    public class KeyWordMatchHelperTests
    {
        [TestMethod()]
        public void IsMatchKeyWordsTest()
        {
            {
                string org = "";
                string kws = "";
                var ret = KeyWordMatchHelper.IsMatchKeyWords(org, kws, out var m, " ", false);
                Assert.IsTrue(ret == false);
                Assert.IsTrue(m == null);
            }
            {
                string org = "abcdefg";
                string kws = "a";
                var ret = KeyWordMatchHelper.IsMatchKeyWords(org, kws, out var m, " ", false);
                Assert.IsTrue(ret == true);
                Assert.IsTrue(m.Count == org.Length && m[0] == true && m[1] == false);
            }
            {
                string org = "abcdefg";
                string kws = "A";
                var ret = KeyWordMatchHelper.IsMatchKeyWords(org, kws, out var m, " ", true);
                Assert.IsTrue(ret == false);
                Assert.IsTrue(m.Count == org.Length && m[0] == false && m[1] == false);
            }
            {
                string org = "abcdefg";
                string kws = "a c";
                var ret = KeyWordMatchHelper.IsMatchKeyWords(org, kws, out var m, " ", true);
                Assert.IsTrue(ret == true);
                Assert.IsTrue(m.Count == org.Length && m[0] == true && m[1] == false && m[2] == true);
            }
            {
                string org = "abcdefgabcdefg";
                string kws = "defg abc";
                var ret = KeyWordMatchHelper.IsMatchKeyWords(org, kws, out var m, " ", true);
                Assert.IsTrue(ret == true);
            }
            {
                string org = "abcdefgabcdefg";
                string kws = "defg abd";
                var ret = KeyWordMatchHelper.IsMatchKeyWords(org, kws, out var m, " ", true);
                Assert.IsTrue(ret == false);
            }
            {
                string org = "abcdefgaaxxabdefg";
                string kws = "defg abd   aaxx ";
                var ret = KeyWordMatchHelper.IsMatchKeyWords(org, kws, out var m, " ", true);
                Assert.IsTrue(ret == true);
            }
            {
                string org = "abcdefgaaxxabd efg";
                string kws = " ";
                var ret = KeyWordMatchHelper.IsMatchKeyWords(org, kws, out var m, " ", true);
                Assert.IsTrue(ret == false);
            }
            {
                string org = "abcdefgaaxxabd efg";
                string kws = " ";
                var ret = KeyWordMatchHelper.IsMatchKeyWords(org, kws, out var m, "", true);
                Assert.IsTrue(ret == true);
            }
        }

        [TestMethod()]
        public void IsMatchPinyinKeyWordsTest()
        {
            {
                string org = "你好世界";
                string kws = "你好";
                var ret = KeyWordMatchHelper.IsMatchPinyinKeyWords(org, kws, out var m, " ", true);
                Assert.IsTrue(ret == true);
                Assert.IsTrue(m.Count == org.Length && m[0] == true && m[1] == true);
            }
            {
                string org = "你好世界";
                string kws = "你 好";
                var ret = KeyWordMatchHelper.IsMatchPinyinKeyWords(org, kws, out var m, " ", true);
                Assert.IsTrue(ret == true);
                Assert.IsTrue(m.Count == org.Length && m[0] == true && m[1] == true);
            }
            {
                string org = "你好世界";
                string kws = "好";
                var ret = KeyWordMatchHelper.IsMatchPinyinKeyWords(org, kws, out var m, " ", false);
                Assert.IsTrue(ret == true);
                Assert.IsTrue(m.Count == org.Length && m[0] == false && m[1] == true);
            }
            {
                string org = "你好世界";
                string kws = "NiHao";
                var ret = KeyWordMatchHelper.IsMatchPinyinKeyWords(org, kws, out var m, " ", false);
                Assert.IsTrue(ret == true);
                Assert.IsTrue(m.Count == org.Length && m[0] == true && m[1] == true);
            }
            {
                string org = "你好世界";
                string kws = "NH";
                var ret = KeyWordMatchHelper.IsMatchPinyinKeyWords(org, kws, out var m, " ", false);
                Assert.IsTrue(ret == true);
                Assert.IsTrue(m.Count == org.Length && m[0] == true && m[1] == true);
            }
            {
                string org = "你好世界";
                string kws = "NH";
                var ret = KeyWordMatchHelper.IsMatchPinyinKeyWords(org, kws, out var m, " ", true);
                Assert.IsTrue(ret == true);
                Assert.IsTrue(m.Count == org.Length && m[0] == true && m[1] == true);
            }
            {
                string org = "你好世界";
                string kws = "N j";
                var ret = KeyWordMatchHelper.IsMatchPinyinKeyWords(org, kws, out var m, " ", false);
                Assert.IsTrue(ret == true);
                Assert.IsTrue(m.Count == org.Length && m[0] == true && m[1] == false && m[2] == false && m[3] == true);
            }
            {
                string org = "你好世界";
                string kws = "N j";
                var ret = KeyWordMatchHelper.IsMatchPinyinKeyWords(org, kws, out var m, " ", true);
                Assert.IsTrue(ret == true);
                Assert.IsTrue(m.Count == org.Length && m[0] == true && m[1] == false && m[2] == false && m[3] == true);
            }
            {
                string org = "你好世界HelloWorld";
                string kws = "sjhello";
                var ret = KeyWordMatchHelper.IsMatchPinyinKeyWords(org, kws, out var m, " ", true);
                Assert.IsTrue(ret == false);
            }
            {
                string org = "你好世界HelloWorld";
                string kws = "SJHello";
                var ret = KeyWordMatchHelper.IsMatchPinyinKeyWords(org, kws, out var m, " ", false);
                Assert.IsTrue(ret == true);
                Assert.IsTrue(m.Count == org.Length && m[0] == false && m[1] == false && m[2] == true && m[3] == true && m[4] == true&& m[5] == true);
            }
            {
                string org = "你好世界HelloWorld";
                string kws = "shi jieh";
                var ret = KeyWordMatchHelper.IsMatchPinyinKeyWords(org, kws, out var m, " ", true);
                Assert.IsTrue(ret == false);
            }
            {
                string org = "你好世界HelloWorld";
                string kws = "shi jieh";
                var ret = KeyWordMatchHelper.IsMatchPinyinKeyWords(org, kws, out var m, " ", false);
                Assert.IsTrue(ret == true);
                Assert.IsTrue(m.Count == org.Length && m[0] == false && m[1] == false && m[2] == true && m[3] == true && m[4] == true);
            }
            {
                // 多音字，不支持
                string org = "行动起来HAHAH一行白鹭";
                string kws = "X D";
                var ret = KeyWordMatchHelper.IsMatchPinyinKeyWords(org, kws, out var m, " ", true);
                Assert.IsTrue(ret == true);
            }
        }
    }
}