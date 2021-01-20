using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Converters;
using PRM.Core.Annotations;
using TinyPinyin.Core;

namespace Shawn.Utils
{
    public static class KeyWordMatchHelper
    {
        public static bool IsMatchKeywords(this string orgString, [NotNull] string keyword, out List<bool> matchPlace, string keywordSeparator = " ", bool isCaseSensitive = false)
        {
            if (string.IsNullOrEmpty(keywordSeparator))
                return IsMatchKeywords(orgString, new[] { keyword }, out matchPlace, isCaseSensitive);
            var kws = keyword.Split(new string[] { keywordSeparator }, StringSplitOptions.RemoveEmptyEntries);
            return IsMatchKeywords(orgString, kws, out matchPlace, isCaseSensitive);
        }

        /// <summary>
        /// 返回关键词是否全部存在于原始字符串中，同时输出匹配位置
        /// </summary>
        /// <param name="orgString"></param>
        /// <param name="keywords"></param>
        /// <param name="matchPlace"></param>
        /// <param name="isCaseSensitive">大小写敏感</param>
        /// <returns></returns>
        public static bool IsMatchKeywords(this string orgString, [NotNull] IEnumerable<string> keywords, out List<bool> matchPlace, bool isCaseSensitive = false)
        {
            matchPlace = null;
            if (string.IsNullOrEmpty(orgString))
                return false;
            var keywordList = keywords.ToList();
            if (!keywordList.Any())
                return false;


            var orgStringInTrueCase = orgString;
            if (isCaseSensitive == false)
                orgStringInTrueCase = orgStringInTrueCase.ToLower();

            // 找到一个占位符，创建占位字符串，然后用 replace 把所有匹配的位置标记为占位字符串
            var placeholder = FindPlaceholder(orgStringInTrueCase);
            if (placeholder == 0)
                return false;

            bool isAllKeyWordMatched = true;
            matchPlace = new List<bool>(new bool[orgString.Length]);
            foreach (var t in keywordList)
            {
                if (string.IsNullOrEmpty(t))
                    continue;
                var keyword = t;
                if (isCaseSensitive == false)
                    keyword = keyword.ToLower();
                if (orgStringInTrueCase.IndexOf(keyword, StringComparison.Ordinal) >= 0)
                {
                    var placeholderString = new string(placeholder, keyword.Length);
                    // 标记所有匹配位置
                    var tmpString = orgStringInTrueCase.Replace(keyword, placeholderString);
                    for (var i = 0; i < tmpString.Length; i++)
                    {
                        var @char = tmpString[i];
                        if (@char == placeholder)
                        {
                            matchPlace[i] = true;
                        }
                    }
                }
                else
                {
                    isAllKeyWordMatched = false;
                    break;
                }
            }

            if (isAllKeyWordMatched)
                return true;

            matchPlace = null;
            return false;
        }


        /// <summary>
        /// 汉字全拼音匹配
        /// </summary>
        /// <param name="orgString"></param>
        /// <param name="keyword"></param>
        /// <param name="matchPlace"></param>
        /// <param name="keywordSeparator"></param>
        /// <param name="isCaseSensitive"></param>
        /// <returns></returns>
        public static bool IsMatchPinyinKeywords(this string orgString, [NotNull] string keyword, out List<bool> matchPlace, string keywordSeparator = " ", bool isCaseSensitive = false)
        {
            if (string.IsNullOrEmpty(keywordSeparator))
                return IsMatchPinyinKeywords(orgString, new string[] { keyword }, out matchPlace, isCaseSensitive);
            var kws = keyword.Split(new string[] { keywordSeparator }, StringSplitOptions.RemoveEmptyEntries);
            return IsMatchPinyinKeywords(orgString, kws, out matchPlace, isCaseSensitive);
        }


        /// <summary>
        /// 汉字拼音首字母匹配
        /// </summary>
        /// <param name="orgString"></param>
        /// <param name="keyword"></param>
        /// <param name="matchPlace"></param>
        /// <param name="keywordSeparator"></param>
        /// <param name="isCaseSensitive"></param>
        /// <returns></returns>
        public static bool IsMatchPinyinInitialKeywords(this string orgString, [NotNull] string keyword, out List<bool> matchPlace, string keywordSeparator = " ", bool isCaseSensitive = false)
        {
            if (string.IsNullOrEmpty(keywordSeparator))
                return IsMatchPinyinKeywords(orgString, new string[] { keyword }, out matchPlace, isCaseSensitive, true);
            var kws = keyword.Split(new string[] { keywordSeparator }, StringSplitOptions.RemoveEmptyEntries);
            return IsMatchPinyinKeywords(orgString, kws, out matchPlace, isCaseSensitive, true);
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
        private static bool IsMatchPinyinKeywords(this string orgString, [NotNull] IEnumerable<string> keywords, out List<bool> matchPlace, bool isCaseSensitive = false, bool isInitialOnly = false)
        {
            matchPlace = null;
            if (string.IsNullOrEmpty(orgString))
                return false;
            var keywordList = keywords.ToList();
            if (!keywordList.Any())
                return false;

            var keywordMatchedFlags = new List<bool>(keywordList.Count);
            for (int i = 0; i < keywordList.Count; i++)
                keywordMatchedFlags.Add(false);

            matchPlace = new List<bool>(orgString.Length);
            var matchPlaceEn = new List<bool>(orgString.Length);
            var matchPlacePinyinInitials = new List<bool>(orgString.Length);
            var matchPlacePinyinAll = new List<bool>(orgString.Length);
            for (int i = 0; i < orgString.Length; i++)
            {
                matchPlace.Add(false);
                matchPlaceEn.Add(false);
                matchPlacePinyinInitials.Add(false);
                matchPlacePinyinAll.Add(false);
            }


            // 原始字符匹配(英文匹配)
            for (var i = 0; i < keywordList.Count; i++)
            {
                var keyword = keywordList[i];
                if (IsMatchKeywords(orgString, new[] { keyword }, out var m1, isCaseSensitive))
                {
                    keywordMatchedFlags[i] = true;
                    for (int j = 0; j < m1.Count; j++)
                    {
                        matchPlaceEn[j] |= m1[j];
                    }
                }
            }


            // 没有汉字时，直接返回英文匹配结果
            if (orgString.All(@char => !PinyinHelper.IsChinese(@char)))
            {
                goto Return;
            }




            // 找到一个占位符，创建占位字符串，然后用 replace 把所有匹配的位置标记为占位字符串
            var placeholder = FindPlaceholder(isCaseSensitive ? orgString : orgString.ToLower());
            if (placeholder == 0)
                return false;



            // 标记哪里是汉字哪里是英文，为 true 的地方表示是汉字
            var pinyinFlagOnOrgString = new List<bool>();
            foreach (var @char in orgString)
            {
                if (PinyinHelper.IsChinese(@char))
                {
                    var pinyin = PinyinHelper.GetPinyin(@char).ToLower();
                    pinyinFlagOnOrgString.Add(true);
                }
                else
                {
                    pinyinFlagOnOrgString.Add(false);
                }
            }



            // 进行拼音首字母的匹配
            {
                string pinyinInitialsString = PinyinHelper.GetPinyinInitials(orgString);
                // 先用小写匹配所有词，再根据大小写敏感校验英文字符是否匹配
                var pinyinInitialsLowerString = pinyinInitialsString.ToLower();
                for (var n = 0; n < keywordList.Count; n++)
                {
                    var keyword = keywordList[n];
                    if (string.IsNullOrEmpty(keyword))
                        continue;

                    if (isCaseSensitive == false)
                    {
                        // 大小写不敏感时，直接标记所有匹配位置
                        if (IsMatchKeywords(pinyinInitialsLowerString, keyword, out var m1, "", false))
                        {
                            keywordMatchedFlags[n] = true;
                            for (int i = 0; i < m1.Count; i++)
                            {
                                if (m1[i])
                                    matchPlacePinyinInitials[i] = true;
                            }
                        }
                    }
                    else
                    {
                        // 大小写敏感时，先找出全小写模式下的匹配位置，再判断这些匹配位置处非汉字字符的大小写匹配情况
                        if (IsMatchKeywords(pinyinInitialsLowerString, keyword, out var m1, "", false))
                        {
                            for (int i = 0; i <= m1.Count - keyword.Length; i++)
                            {
                                // 如果此处匹配 keyword[0]，则进入继续匹配 keyword[0] - keyword[keyword.Count - 1]
                                if (m1[i]
                                    && ((!pinyinFlagOnOrgString[i] && string.Equals(orgString[i].ToString(),
                                             keyword[0].ToString(), StringComparison.CurrentCultureIgnoreCase))
                                        || (pinyinFlagOnOrgString[i] && string.Equals(
                                                pinyinInitialsLowerString[i].ToString(), keyword[0].ToString(),
                                                StringComparison.CurrentCultureIgnoreCase))))
                                {
                                    bool isThisPlaceMatched = true;
                                    for (int j = 0; j < keyword.Length; j++)
                                    {
                                        // 匹配且是英文的位置，验证大小写是否匹配
                                        if (!pinyinFlagOnOrgString[i + j]
                                            && orgString[i + j] != keyword[j])
                                        {
                                            isThisPlaceMatched = false;
                                            break;
                                        }
                                    }

                                    // 任一位置匹配成功后，标记当前 keyword 匹配成功
                                    if (isThisPlaceMatched == true)
                                    {
                                        keywordMatchedFlags[n] = true;
                                        for (var j = 0; j < keyword.Length; j++)
                                        {
                                            matchPlacePinyinInitials[i + j] = true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }




            // 进行拼音全词匹配
            if (isInitialOnly == false)
            {
                var charFullPinYinList = new List<string>();
                for (var i = 0; i < orgString.Length; i++)
                {
                    var @char = orgString[i];
                    if (pinyinFlagOnOrgString[i])
                    {
                        var pinyin = PinyinHelper.GetPinyin(@char).ToLower();
                        charFullPinYinList.Add(pinyin);
                    }
                    else
                    {
                        charFullPinYinList.Add(@char.ToString());
                    }
                }

                // 拼音字符串中字符，对应原始汉字字符的索引，非拼音首字母字符索引为负数，例如 "你好世界2020" 的拼音 "nihaoshijie2020"，其索引为 [0,-1,  1,-1,-1,   2,-1,-1,   3,-1,-1,  4,5,6,7]
                var pinyinChar2OrgCharIndex = new List<int>();
                var sb = new StringBuilder();  // 构建拼音字符串
                for (var i = 0; i < charFullPinYinList.Count; ++i)
                {
                    var c = charFullPinYinList[i];
                    sb.Append(c);
                    if (c.Length == 1)
                    {
                        pinyinChar2OrgCharIndex.Add(i);
                    }
                    else
                    {
                        pinyinChar2OrgCharIndex.Add(i);
                        for (int j = 1; j < c.Length; ++j)
                        {
                            pinyinChar2OrgCharIndex.Add(-1);
                        }
                    }
                }

                // 先用小写匹配所有词，再根据大小写敏感校验英文字符是否匹配
                var pinyinLowerString = sb.ToString().ToLower();
                matchPlace = new List<bool>(new bool[orgString.Length]);
                for (var n = 0; n < keywordList.Count; n++)
                {
                    var keyword = keywordList[n];
                    if (string.IsNullOrEmpty(keyword))
                        continue;

                    if (isCaseSensitive == false)
                    {
                        // 大小写不敏感时，直接标记所有匹配位置
                        if (IsMatchKeywords(pinyinLowerString, keyword, out var m1, "", false))
                        {
                            keywordMatchedFlags[n] = true;
                            for (int i = 0; i < m1.Count; i++)
                            {
                                if (m1[i]
                                    && pinyinChar2OrgCharIndex[i] >= 0)
                                    matchPlacePinyinInitials[pinyinChar2OrgCharIndex[i]] = true;
                            }
                        }
                    }
                    else
                    {
                        // 大小写敏感时，先找出全小写模式下的匹配位置，再判断这些匹配位置处非汉字字符的大小写匹配情况
                        if (IsMatchKeywords(pinyinLowerString, keyword, out var m1, "", isCaseSensitive))
                        {
                            for (int i = 0; i <= m1.Count - keyword.Length; i++)
                            {
                                // 如果此处匹配 keyword[0]，则进入继续匹配 keyword[0] - keyword[keyword.Count - 1]
                                if (m1[i]
                                    && pinyinChar2OrgCharIndex[i] >= 0
                                    && ((!pinyinFlagOnOrgString[pinyinChar2OrgCharIndex[i]] &&
                                         string.Equals(orgString[pinyinChar2OrgCharIndex[i]].ToString(),
                                             keyword[0].ToString(), StringComparison.CurrentCultureIgnoreCase))
                                        || (pinyinFlagOnOrgString[pinyinChar2OrgCharIndex[i]] &&
                                            string.Equals(pinyinLowerString[i].ToString(), keyword[0].ToString(),
                                                StringComparison.CurrentCultureIgnoreCase))))
                                {
                                    bool isThisPlaceMatched = true;
                                    for (int j = 0; j < keyword.Length; j++)
                                    {
                                        // 匹配且是英文的位置，验证大小写是否匹配
                                        if (!pinyinFlagOnOrgString[pinyinChar2OrgCharIndex[i] + j]
                                            && orgString[pinyinChar2OrgCharIndex[i] + j] != keyword[j])
                                        {
                                            isThisPlaceMatched = false;
                                            break;
                                        }
                                    }

                                    // 任一位置匹配成功后，标记当前 keyword 匹配成功
                                    if (isThisPlaceMatched == true)
                                    {
                                        keywordMatchedFlags[n] = true;
                                        for (var j = 0; j < keyword.Length; j++)
                                        {
                                            if (pinyinChar2OrgCharIndex[i + j] >= 0)
                                            {
                                                int k = pinyinChar2OrgCharIndex[i + j];
                                                matchPlacePinyinInitials[k] = true;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

        Return:

            if (keywordMatchedFlags.All(x => x == true))
            {
                for (int i = 0; i < orgString.Length; i++)
                {
                    matchPlace[i] = (matchPlaceEn?[i] ?? false) | matchPlacePinyinInitials[i] | matchPlacePinyinAll[i];
                }
                return true;
            }

            matchPlace = null;
            return false;
        }

        /// <summary>
        /// 找到原始字符串中不存在的字符作为占位符，如果找不到，则返回 (char)0
        /// </summary>
        /// <param name="orgString"></param>
        /// <returns></returns>
        private static char FindPlaceholder(string orgString)
        {
            // 找到原始字符串中不存在的字符作为占位符
            char placeholder = (char)7;
            while (orgString.IndexOf(placeholder) >= 0
                   && placeholder < 65530)
            {
                ++placeholder;
            }
            if (placeholder >= 65530)
                return (char)0;
            return placeholder;
        }
    }
}
