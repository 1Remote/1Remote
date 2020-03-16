using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Converters;

namespace PRM.Core.Ulits
{
    static class KeyWordMatchHelper
    {
        public static bool IsMatch(this string orgString, string keyWord)
        {
            bool hasChinese = orgString.Any(TinyPinyin.PinyinHelper.IsChinese);
            if (hasChinese)
            {

            }
            else
            {

            }
            return false;
        }


        /// <summary>
        /// 返回英文关键词是否全部存在于原始英文字符串中，同时输出匹配位置
        /// </summary>
        /// <param name="orgString"></param>
        /// <param name="keywords"></param>
        /// <param name="matchPlace"></param>
        /// <param name="isCaseSensitive">大小写敏感</param>
        /// <returns></returns>
        private static bool IsStringMatchKeyWords(this string orgString, IEnumerable<string> keywords, out List<bool> matchPlace, bool isCaseSensitive = false)
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
                    for (int i = n; i < keyword.Length; i++)
                    {
                        matchPlace[i] = true;
                    }
                }
                else
                    return false;
            }
            return matchPlace.Any(x => x == true);
        }

        private static bool IsChsStringMatchKeyWords(this string orgString, IEnumerable<string> keywords, out List<bool> matchPlace, bool isCaseSensitive = false)
        {
            matchPlace = null;
            if (string.IsNullOrEmpty(orgString))
                return false;
            if (!keywords.Any())
                return false;

            var tmpOrgString = (string)orgString.Clone();
            if (isCaseSensitive == false)
                tmpOrgString = tmpOrgString.ToLower();

            bool hasChinese = false;
            var charList = new List<string>();
            foreach (var @char in orgString)
            {
                if (TinyPinyin.PinyinHelper.IsChinese(@char))
                {
                    charList.Add(TinyPinyin.PinyinHelper.GetPinyin(@char).ToLower());
                    hasChinese = true;
                }
                else
                {
                    charList.Add(@char.ToString());
                }
            }

            if (!hasChinese)
            {
                return IsStringMatchKeyWords(orgString, keywords, out matchPlace, isCaseSensitive);
            }

            string pinyinString = TinyPinyin.PinyinHelper.GetPinyin(orgString, "");
            matchPlace = new List<bool>(new bool[orgString.Length]);
            foreach (var keyword in keywords)
            {
                if (string.IsNullOrEmpty(keyword))
                    continue;
                int n = tmpOrgString.IndexOf(isCaseSensitive ? keyword : keyword.ToLower());
                if (n >= 0)
                {
                    for (int i = n; i < keyword.Length; i++)
                    {
                        matchPlace[i] = true;
                    }
                }
                else
                    return false;
            }
            return matchPlace.Any(x => x == true);

        }
    }
}
