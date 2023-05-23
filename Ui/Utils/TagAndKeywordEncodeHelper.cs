using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using VariableKeywordMatcher.Model;

namespace _1RM.Utils
{
    public static class TagAndKeywordEncodeHelper
    {
        public static string RectifyTagName(string name)
        {
            return name.Replace("#", "").Replace(" ", "-").Trim().ToLower();
        }

        public class KeywordDecoded
        {
            /// <summary>
            /// support include and exclude tag filter
            /// </summary>
            public List<TagFilter> TagFilterList = new List<TagFilter>();

            /// <summary>
            /// tag name start with incomplete word from user type; 
            /// support type #lin to list all tags who start by #lin
            /// </summary>
            public List<string> IncludeTagsStartWithKeyWord = new List<string>();

            /// <summary>
            /// keywords to matching server name address username etc..
            /// </summary>
            public List<string> KeyWords = new List<string>();

            public bool IsKeywordEmpty()
            {
                return IncludeTagsStartWithKeyWord?.Any() != true && TagFilterList?.Any() != true && KeyWords?.Any() != true;
            }
        }

        public static KeywordDecoded DecodeKeyword(string keyword)
        {
            var words = keyword.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries).ToList();
            var ret = new KeywordDecoded();
            for (var i = 0; i < words.Count; i++)
            {
                var word = words[i];
                if (word.StartsWith("#") || word.StartsWith("-#") || word.StartsWith("+#"))
                {
                    bool isExcluded = word.StartsWith("-#");
                    string tagName = word.Substring(word.IndexOf("#", StringComparison.Ordinal) + 1);
                    if (string.IsNullOrWhiteSpace(tagName))
                        continue;

                    // 完整输入 name 的 tag
                    if (IoC.Get<GlobalData>().TagList.Any(x => string.Equals(x.Name, tagName, StringComparison.CurrentCultureIgnoreCase)))
                    {
                        ret.TagFilterList.Add(TagFilter.Create(tagName, isExcluded ? TagFilter.FilterType.Excluded : TagFilter.FilterType.Included));
                    }
                    // 不完整输入 name 部分匹配的 tag, works only with included
                    else if (isExcluded == false)
                    {
                        var tmp = IoC.Get<GlobalData>().TagList.Where(x => x.Name.StartsWith(tagName, StringComparison.OrdinalIgnoreCase)).Select(x => x.Name);
                        ret.IncludeTagsStartWithKeyWord.AddRange(tmp);
                    }
                }
                else if (string.IsNullOrEmpty(word) == false)
                {
                    ret.KeyWords.Add(word);
                }
            }

            ret.IncludeTagsStartWithKeyWord = ret.IncludeTagsStartWithKeyWord.Distinct().ToList();

            return ret;
        }
        public static string EncodeKeyword(List<TagFilter>? tagFilters = null, List<string>? keyWords = null)
        {
            StringBuilder sb = new StringBuilder();
            if (tagFilters != null)
                foreach (var tag in tagFilters.Distinct())
                {
                    if (tag.IsExcluded)
                        sb.Append(" -#" + tag.TagName);
                    else
                        sb.Append(" #" + tag.TagName);
                }

            if (keyWords != null)
                foreach (var keyWord in keyWords.Distinct())
                {
                    sb.Append(" " + keyWord);
                }

            var filterString = sb.ToString().Trim();
            if (string.IsNullOrWhiteSpace(filterString))
                return "";
            return filterString + (keyWords?.Count > 0 ? "" : " ");
        }
        public static Tuple<bool, MatchResults?> MatchKeywords(ProtocolBase server, KeywordDecoded keywordDecoded, bool matchSubTitle)
        {
            if (keywordDecoded.IsKeywordEmpty())
            {
                return new Tuple<bool, MatchResults?>(true, null);
            }

            // check include and excluded tags
            if (keywordDecoded.TagFilterList.Any() == true)
            {
                // check with and logic，必须全部匹配才通过（任意不匹配就不通过）
                bool bTagMatched = true;
                foreach (var tagFilter in keywordDecoded.TagFilterList)
                {
                    if (tagFilter.IsIncluded != server.Tags.Any(x => String.Equals(x, tagFilter.TagName, StringComparison.CurrentCultureIgnoreCase)))
                    {
                        bTagMatched = false;
                        break;
                    }
                }

                if (bTagMatched == false)
                {
                    return new Tuple<bool, MatchResults?>(false, null);
                }
            }

            // check tag name start with incomplete word from user type
            if (keywordDecoded.IncludeTagsStartWithKeyWord.Any() == true)
            {
                // check with or logic，只要任意匹配就通过
                bool bTagMatched = false;
                foreach (var tagName in keywordDecoded.IncludeTagsStartWithKeyWord)
                {
                    if (server.Tags.Any(x => String.Equals(x, tagName, StringComparison.CurrentCultureIgnoreCase)))
                    {
                        bTagMatched = true;
                        break;
                    }
                }

                if (bTagMatched == false)
                {
                    return new Tuple<bool, MatchResults?>(false, null);
                }
            }

            // no keyword
            if (keywordDecoded.KeyWords.Any() != true)
            {
                return new Tuple<bool, MatchResults?>(true, null);
            }

            // match keywords
            var dispName = server.DisplayName;
            var subTitle = server.SubTitle;
            if (matchSubTitle == false)
            {
                subTitle = "";
            }
            var mrs = IoC.Get<KeywordMatchService>().Match(new List<string>() { dispName, subTitle }, keywordDecoded.KeyWords);
            if (mrs.IsMatchAllKeywords)
                return new Tuple<bool, MatchResults?>(true, mrs);

            return new Tuple<bool, MatchResults?>(false, null);
        }
    }
}
