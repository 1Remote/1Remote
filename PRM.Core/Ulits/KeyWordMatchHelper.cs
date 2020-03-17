using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Converters;
using PRM.Core.Annotations;

namespace PRM.Core.Ulits
{
    public static class KeyWordMatchHelper
    {
        public static bool IsMatchKeyWords(this string orgString, [NotNull] string keyword, out List<bool> matchPlace, string keyWordSeparator = " ", bool isCaseSensitive = false)
        {
            if (string.IsNullOrEmpty(keyWordSeparator))
                return IsMatchKeyWords(orgString, new[] { keyword }, out matchPlace, isCaseSensitive);
            var kws = keyword.Split(new string[]{keyWordSeparator}, StringSplitOptions.RemoveEmptyEntries);
            return IsMatchKeyWords(orgString, kws, out matchPlace, isCaseSensitive);
        }

        /// <summary>
        /// 返回关键词是否全部存在于原始字符串中，同时输出匹配位置
        /// </summary>
        /// <param name="orgString"></param>
        /// <param name="keywords"></param>
        /// <param name="matchPlace"></param>
        /// <param name="isCaseSensitive">大小写敏感</param>
        /// <returns></returns>
        public static bool IsMatchKeyWords(this string orgString, [NotNull] IEnumerable<string> keywords, out List<bool> matchPlace, bool isCaseSensitive = false)
        {
            matchPlace = null;
            if (string.IsNullOrEmpty(orgString))
                return false;
            if (!keywords.Any())
                return false;

            var tmpOrgString = (string)orgString.Clone();
            if (isCaseSensitive == false)
                tmpOrgString = tmpOrgString.ToLower();
            matchPlace = new List<bool>(new bool[orgString.Length]);
            foreach (var keyword in keywords)
            {
                if (string.IsNullOrEmpty(keyword))
                    continue;
                int n = tmpOrgString.IndexOf(isCaseSensitive ? keyword : keyword.ToLower());
                if (n >= 0)
                {
                    for (int i = n; i < n + keyword.Length; i++)
                    {
                        matchPlace[i] = true;
                    }
                }
                else
                {
                    matchPlace = new List<bool>(new bool[orgString.Length]);
                    return false;
                }
            }
            return matchPlace.Any(x => x == true);
        }


        /// <summary>
        /// 汉字全拼音匹配
        /// </summary>
        /// <param name="orgString"></param>
        /// <param name="keyword"></param>
        /// <param name="matchPlace"></param>
        /// <param name="isCaseSensitive"></param>
        /// <returns></returns>
        public static bool IsMatchPinyinKeyWords(this string orgString, [NotNull] string keyword, out List<bool> matchPlace, string keyWordSeparator = " ", bool isCaseSensitive = false)
        {
            if (string.IsNullOrEmpty(keyWordSeparator))
                return IsMatchPinyinKeyWords(orgString, new string[] { keyword }, out matchPlace, isCaseSensitive);
            var kws = keyword.Split(new string[]{keyWordSeparator}, StringSplitOptions.RemoveEmptyEntries);
            return IsMatchPinyinKeyWords(orgString, kws, out matchPlace, isCaseSensitive, true);
        }

        
        /// <summary>
        /// 汉字拼音首字母匹配
        /// </summary>
        /// <param name="orgString"></param>
        /// <param name="keyword"></param>
        /// <param name="matchPlace"></param>
        /// <param name="isCaseSensitive"></param>
        /// <returns></returns>
        public static bool IsMatchPinyinInitialKeyWords(this string orgString, [NotNull] string keyword, out List<bool> matchPlace, string keyWordSeparator = " ", bool isCaseSensitive = false)
        {
            if (string.IsNullOrEmpty(keyWordSeparator))
                return IsMatchPinyinKeyWords(orgString, new string[] { keyword }, out matchPlace, isCaseSensitive);
            var kws = keyword.Split(new string[]{keyWordSeparator}, StringSplitOptions.RemoveEmptyEntries);
            return IsMatchPinyinKeyWords(orgString, kws, out matchPlace, isCaseSensitive, false);
        }


        /// <summary>
        /// 字符串中的汉字同时会被转为拼音进行匹配
        /// </summary>
        /// <param name="orgString"></param>
        /// <param name="keywords"></param>
        /// <param name="matchPlace"></param>
        /// <param name="isCaseSensitive"></param>
        /// <param name="isInitialOnly">是否用只用拼音首字母匹配</param>
        /// <returns></returns>
        public static bool IsMatchPinyinKeyWords(this string orgString, [NotNull] IEnumerable<string> keywords, out List<bool> matchPlace, bool isCaseSensitive = false, bool isInitialOnly = false)
        {
            matchPlace = null;
            if (string.IsNullOrEmpty(orgString))
                return false;
            if (!keywords.Any())
                return false;

            var charFullPinYinList = new List<string>();
            var charInitialPinYinList = new List<string>();
            var pinyinFlagOnOrgString = new List<bool>();
            foreach (var @char in orgString)
            {
                if (TinyPinyin.PinyinHelper.IsChinese(@char))
                {
                    charFullPinYinList.Add(TinyPinyin.PinyinHelper.GetPinyin(@char).ToLower());
                    charInitialPinYinList.Add(TinyPinyin.PinyinHelper.GetPinyinInitials(@char.ToString()).ToLower());
                    pinyinFlagOnOrgString.Add(true);
                }
                else
                {
                    charFullPinYinList.Add(isCaseSensitive ? @char.ToString() : @char.ToString().ToLower());
                    charInitialPinYinList.Add(isCaseSensitive ? @char.ToString() : @char.ToString().ToLower());
                    pinyinFlagOnOrgString.Add(false);
                }
            }

            var isDirectMatch = IsMatchKeyWords(orgString, keywords, out matchPlace, isCaseSensitive);

            // 直接匹配成功或未发现汉字时，直接返回匹配结果
            if (pinyinFlagOnOrgString.All(x => x == false) || isDirectMatch == true)
            {
                return isDirectMatch;
            }

            // 否则进行拼音首字母的匹配
            string pinyinInitialsString = TinyPinyin.PinyinHelper.GetPinyinInitials(orgString);
            if (pinyinInitialsString.Length == orgString.Length)
            {
                // 先用小写匹配所有词，再根据大小写敏感校验英文字符是否匹配
                var pinyinInitialsLowerString = pinyinInitialsString.ToLower();
                matchPlace = new List<bool>(new bool[orgString.Length]);
                foreach (var keyword in keywords)
                {
                    if (string.IsNullOrEmpty(keyword))
                        continue;
                    int n = pinyinInitialsLowerString.IndexOf(keyword.ToLower());
                    if (n >= 0)
                    {
                        if (isCaseSensitive == true)
                        {
                            // 非英文部分校验大小写匹配
                            for (int i = 0; i < keyword.Length; i++)
                            {
                                if (pinyinFlagOnOrgString[n + i] == false &&
                                    orgString[n + i] != keyword[i])
                                {
                                    n = -1;
                                }
                            }
                        }
                    }
                    if (n >= 0)
                    {
                        for (int i = n; i < n + keyword.Length; i++)
                        {
                            matchPlace[i] = true;
                        }
                    }
                    else
                    {
                        matchPlace = new List<bool>(new bool[orgString.Length]);
                        break;
                    }
                }
                if (matchPlace.Any(x => x == true))
                    return true;
            }




            // 否则进行拼音全词匹配
            if(isInitialOnly)
            {
                // 拼音字符串中字符，对应原始汉字字符的索引，非拼音首字母字符索引为负数，例如 "你好世界2020" 的拼音 "nihaoshijie2020"，其索引为 [0,-1,  1,-1,-1,   2,-1,-1,   3,-1,-1,  4,5,6,7]
                var pinyinChar2OrgCharIndex = new List<int>();
                var pinyinFlagOnPinyinString = new List<bool>();
                StringBuilder sb = new StringBuilder();  // 构建拼音字符串
                for (var i = 0; i < charFullPinYinList.Count; ++i)
                {
                    var c = charFullPinYinList[i];
                    sb.Append(c);
                    if (c.Length == 1)
                    {
                        pinyinChar2OrgCharIndex.Add(i);
                        pinyinFlagOnPinyinString.Add(false);
                    }
                    else
                    {
                        pinyinChar2OrgCharIndex.Add(i);
                        pinyinFlagOnPinyinString.Add(true);
                        for (int j = 1; j < c.Length; ++j)
                        {
                            pinyinChar2OrgCharIndex.Add(-1);
                            pinyinFlagOnPinyinString.Add(true);
                        }
                    }
                }

                // 先用小写匹配所有词，再根据大小写敏感校验英文字符是否匹配
                var tmpPinYinString = sb.ToString().ToLower();
                matchPlace = new List<bool>(new bool[orgString.Length]);
                foreach (var keyword in keywords)
                {
                    if (string.IsNullOrEmpty(keyword))
                        continue;
                    int index1OnPy = tmpPinYinString.IndexOf(keyword.ToLower());
                    if (index1OnPy >= 0 && pinyinChar2OrgCharIndex[index1OnPy] >= 0)
                    {
                        if (isCaseSensitive)
                        {
                            // 非英文部分校验大小写匹配
                            for (int i = 0; i < keyword.Length; i++)
                            {
                                if (pinyinFlagOnPinyinString[index1OnPy + i] == false &&
                                    pinyinChar2OrgCharIndex[index1OnPy + i] >= 0 &&
                                    orgString[pinyinChar2OrgCharIndex[index1OnPy + i]] != keyword[i])
                                {
                                    matchPlace = new List<bool>(new bool[orgString.Length]);
                                    return false;
                                }
                            }
                        }

                        // 查找关键词在原始字符串的起始坐标，标记匹配位置
                        int index1OnOrg = pinyinChar2OrgCharIndex[index1OnPy];
                        int index2OnOrg = -1;
                        int offset = keyword.Length;
                        while (index2OnOrg < 0)
                        {
                            index2OnOrg = pinyinChar2OrgCharIndex[index1OnPy + offset];
                            --offset;
                        }
                        for (int i = index1OnOrg; i <= index2OnOrg; i++)
                        {
                            matchPlace[i] = true;
                        }
                    }
                    else
                        return false;
                }
            }
            return matchPlace.Any(x => x == true);
        }
    }
}
