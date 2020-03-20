using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Converters;
using PRM.Core.Annotations;

namespace Shawn.Ulits
{
    public static class KeyWordMatchHelper
    {
        public static bool IsMatchKeyWords(this string orgString, [NotNull] string keyword, out List<bool> matchPlace, string keyWordSeparator = " ", bool isCaseSensitive = false)
        {
            if (string.IsNullOrEmpty(keyWordSeparator))
                return IsMatchKeyWords(orgString, new[] { keyword }, out matchPlace, isCaseSensitive);
            var kws = keyword.Split(new string[] { keyWordSeparator }, StringSplitOptions.RemoveEmptyEntries);
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
            var keyWordList = keywords.ToList();
            if (!keyWordList.Any())
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
            foreach (var t in keyWordList)
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
        /// <param name="isCaseSensitive"></param>
        /// <returns></returns>
        public static bool IsMatchPinyinKeyWords(this string orgString, [NotNull] string keyword, out List<bool> matchPlace, string keyWordSeparator = " ", bool isCaseSensitive = false)
        {
            if (string.IsNullOrEmpty(keyWordSeparator))
                return IsMatchPinyinKeyWords(orgString, new string[] { keyword }, out matchPlace, isCaseSensitive);
            var kws = keyword.Split(new string[] { keyWordSeparator }, StringSplitOptions.RemoveEmptyEntries);
            return IsMatchPinyinKeyWords(orgString, kws, out matchPlace, isCaseSensitive);
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
                return IsMatchPinyinKeyWords(orgString, new string[] { keyword }, out matchPlace, isCaseSensitive, true);
            var kws = keyword.Split(new string[] { keyWordSeparator }, StringSplitOptions.RemoveEmptyEntries);
            return IsMatchPinyinKeyWords(orgString, kws, out matchPlace, isCaseSensitive, true);
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
            var keyWordList = keywords.ToList();
            if (!keyWordList.Any())
                return false;

            {
                // 原始字符串直接匹配成功时，直接返回
                if (IsMatchKeyWords(orgString, keyWordList, out matchPlace, isCaseSensitive))
                    return true;
            }


            {
                bool hasChinese = false;
                foreach (var @char in orgString)
                {
                    if (TinyPinyin.PinyinHelper.IsChinese(@char))
                    {
                        hasChinese = true;
                        break;
                    }
                }

                // 没有汉字时，匹配失败
                if (!hasChinese)
                {
                    return false;
                }
            }




            // 找到一个占位符，创建占位字符串，然后用 replace 把所有匹配的位置标记为占位字符串
            var placeholder = FindPlaceholder(isCaseSensitive ? orgString : orgString.ToLower());
            if (placeholder == 0)
                return false;



            // 标记哪里是拼音，哪里是汉字
            var charFullPinYinList = new List<string>();
            var pinyinFlagOnOrgString = new List<bool>();
            foreach (var @char in orgString)
            {
                if (TinyPinyin.PinyinHelper.IsChinese(@char))
                {
                    var pinyin = TinyPinyin.PinyinHelper.GetPinyin(@char).ToLower();
                    charFullPinYinList.Add(pinyin);
                    pinyinFlagOnOrgString.Add(true);
                }
                else
                {
                    charFullPinYinList.Add(@char.ToString());
                    pinyinFlagOnOrgString.Add(false);
                }
            }




            // 否则进行拼音首字母的匹配
            {
                string pinyinInitialsString = TinyPinyin.PinyinHelper.GetPinyinInitials(orgString);
                // 先用小写匹配所有词，再根据大小写敏感校验英文字符是否匹配
                var pinyinInitialsLowerString = pinyinInitialsString.ToLower();
                matchPlace = new List<bool>(new bool[orgString.Length]);
                bool isAllKeyWordMatched = true;
                foreach (var keyword in keyWordList)
                {
                    if (string.IsNullOrEmpty(keyword))
                        continue;

                    bool isThisKeyWordDirectMatched = false;
                    if (IsMatchKeyWords(orgString, keyword, out var m1, "", isCaseSensitive))
                    {
                        isThisKeyWordDirectMatched = true;
                        for (int i = 0; i < m1.Count; i++)
                        {
                            if (m1[i])
                                matchPlace[i] = true;
                        }
                    }

                    bool isThisKeyWordMatched = false;
                    if (pinyinInitialsLowerString.IndexOf(keyword.ToLower(), StringComparison.Ordinal) >= 0)
                    {
                        var placeholderString = new string(placeholder, keyword.Length);
                        var tmpString = pinyinInitialsLowerString.Replace(keyword.ToLower(), placeholderString);
                        if (isCaseSensitive == false)
                        {
                            // 大小写不敏感时，直接标记所有匹配位置
                            for (var i = 0; i < tmpString.Length; i++)
                            {
                                var @char = tmpString[i];
                                if (@char == placeholder)
                                {
                                    matchPlace[i] = true;
                                }
                            }
                            isThisKeyWordMatched = true;
                        }
                        else
                        {
                            // 针对大小写敏感的情况，先找出全小写模式下的匹配位置，再判断这些匹配位置处非汉字字符的大小写匹配情况
                            int n = tmpString.IndexOf(placeholderString, StringComparison.Ordinal);
                            int offset = 0;
                            while (n >= 0)
                            {
                                bool isThisPlaceMatched = true;
                                for (int i = 0; i < placeholderString.Length; i++)
                                {
                                    if (!pinyinFlagOnOrgString[i + n + offset] && orgString[i + n + offset] != keyword[i])
                                    {
                                        isThisPlaceMatched = false;
                                        break;
                                    }
                                }

                                if (isThisPlaceMatched)
                                {
                                    isThisKeyWordMatched = true;
                                    // 标记所有匹配位置
                                    for (var i = 0; i < placeholderString.Length; i++)
                                    {
                                        var @char = tmpString[i + n];
                                        if (@char == placeholder)
                                        {
                                            matchPlace[i + n + offset] = true;
                                        }
                                    }
                                }

                                offset += (n + keyword.Length);
                                tmpString = tmpString.Substring(n + keyword.Length);
                                n = tmpString.IndexOf(placeholderString, StringComparison.Ordinal);
                            }
                        }
                    }

                    // 该关键词直接匹配，拼音匹配均不成功时，标记匹配失败
                    if (isThisKeyWordDirectMatched == false && isThisKeyWordMatched == false)
                    {
                        isAllKeyWordMatched = false;
                        break;
                    }
                }
                if (isAllKeyWordMatched)
                    return true;
            }




            // 否则进行拼音全词匹配
            if (isInitialOnly == false)
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
                var pinyinLowerString = sb.ToString().ToLower();
                matchPlace = new List<bool>(new bool[orgString.Length]);
                bool isAllKeyWordMatched = true;
                foreach (var keyword in keyWordList)
                {
                    if (string.IsNullOrEmpty(keyword))
                        continue;

                    bool isThisKeyWordDirectMatched = false;
                    if (IsMatchKeyWords(orgString, keyword, out var m1, "", isCaseSensitive))
                    {
                        isThisKeyWordDirectMatched = true;
                        for (int i = 0; i < m1.Count; i++)
                        {
                            if (m1[i])
                                matchPlace[i] = true;
                        }
                    }

                    bool isThisKeyWordMatched = false;
                    if (pinyinLowerString.IndexOf(keyword.ToLower()) >= 0)
                    {
                        var placeholderString = new string(placeholder, keyword.Length);
                        var tmpString = pinyinLowerString.Replace(keyword.ToLower(), placeholderString);

                        int n = tmpString.IndexOf(placeholderString, StringComparison.Ordinal);
                        int offset = 0;
                        while (n >= 0)
                        {
                            if (pinyinChar2OrgCharIndex[n + offset] >= 0) // 保证关键词是以汉字的拼音首字母开头
                            {
                                if (isCaseSensitive == false)
                                {
                                    isThisKeyWordMatched = true;
                                    // 大小写不敏感时，直接标记所有匹配位置
                                    for (var i = 0; i < placeholderString.Length; i++)
                                    {
                                        if (pinyinChar2OrgCharIndex[i + n + offset] >= 0)
                                        {
                                            var @char = tmpString[i + n];
                                            if (@char == placeholder)
                                            {
                                                matchPlace[pinyinChar2OrgCharIndex[i + n + offset]] = true;
                                            }
                                        }
                                    }
                                }

                                if (isCaseSensitive)
                                {
                                    // 针对大小写敏感的情况，先找出全小写模式下的匹配位置，再判断这些匹配位置处非汉字字符的大小写匹配情况
                                    bool isThisPlaceMatched = true;
                                    // 非英文部分校验大小写匹配
                                    for (int i = 0; i < keyword.Length; i++)
                                    {
                                        if (pinyinFlagOnPinyinString[i + n + offset] == false && // 该位置不是汉字
                                            pinyinChar2OrgCharIndex[i + n + offset] >= 0 && // 该位置对应原始字符串的字符
                                            orgString[pinyinChar2OrgCharIndex[i + n + offset]] != keyword[i])
                                        {
                                            isThisPlaceMatched = false;
                                            break;
                                        }
                                    }

                                    if (isThisPlaceMatched)
                                    {
                                        isThisKeyWordMatched = true;
                                        // 标记所有匹配位置
                                        for (var i = 0; i < placeholderString.Length; i++)
                                        {
                                            if (pinyinChar2OrgCharIndex[i + n + offset] >= 0)
                                            {
                                                var @char = tmpString[i + n];
                                                if (@char == placeholder)
                                                {
                                                    matchPlace[pinyinChar2OrgCharIndex[i + n + offset]] = true;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            offset += (n + keyword.Length);
                            tmpString = tmpString.Substring(n + keyword.Length);
                            n = tmpString.IndexOf(placeholderString, StringComparison.Ordinal);
                        }

                    }

                    // 该关键词直接匹配，拼音匹配均不成功时，标记匹配失败
                    if (isThisKeyWordDirectMatched == false && isThisKeyWordMatched == false)
                    {
                        isAllKeyWordMatched = false;
                        break;
                    }
                }
                if (isAllKeyWordMatched)
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
