using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PRM.Core.Protocol;
using Shawn.Utils;
using VariableKeywordMatcher.Model;

namespace PRM.Utils.Filters
{
    public static class TagAndKeywordFilter
    {
        public static string RectifyTagName(string name)
        {
            return name.Replace("#", "").Trim();
            // return name.Replace(" ", "_").Replace("#", "").Trim();
        }
        public static Tuple<List<TagFilter>, List<string>> DecodeKeyword(string keyword)
        {
            var words = keyword.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries).ToList();
            var keyWords = new List<string>(words.Count);
            var tagFilters = new List<TagFilter>();
            //string prefix = "";
            for (var i = 0; i < words.Count; i++)
            {
                var word = words[i];
                if (word.StartsWith("#") || word.StartsWith("-#") || word.StartsWith("+#"))
                {
                    bool isExcluded = word.StartsWith("-#");
                    string tagName = word.Substring(word.IndexOf("#", StringComparison.Ordinal) + 1);
                    if (App.Context.AppData.TagList.Any(x => string.Equals(x.Name, tagName, StringComparison.CurrentCultureIgnoreCase)))
                    {
                        tagFilters.Add(TagFilter.Create(tagName, isExcluded ? TagFilter.FilterType.Excluded : TagFilter.FilterType.Included));
                    }
                    // 考虑带空格的 tag 情况，遍历后续所有的 word，组装后查询是否是 tag
                    for (int j = i + 1; j < words.Count; j++)
                    {
                        if (words[j].StartsWith("#") || words[j].StartsWith("-#") || words[j].StartsWith("+#"))
                            break;
                        tagName = $"{tagName} {words[j]}";
                        if (App.Context.AppData.TagList.Any(x => string.Equals(x.Name, tagName, StringComparison.CurrentCultureIgnoreCase)))
                        {
                            words[j] = "";
                            tagFilters.Add(TagFilter.Create(tagName, isExcluded ? TagFilter.FilterType.Excluded : TagFilter.FilterType.Included));
                        }
                    }
                }
                else if(string.IsNullOrEmpty(word) == false)
                {
                    keyWords.Add(word);
                }
            }

            return new Tuple<List<TagFilter>, List<string>>(tagFilters, keyWords);
        }


        public static string EncodeKeyword(List<TagFilter> tagFilters, List<string> keyWords)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var tag in tagFilters.Distinct())
            {
                if (tag.IsExcluded)
                    sb.Append(" -#" + tag.TagName);
                else
                    sb.Append(" #" + tag.TagName);
            }

            foreach (var keyWord in keyWords.Distinct())
            {
                sb.Append(" " + keyWord);
            }
            return sb.ToString().Trim();
        }

        public static Tuple<bool, MatchResults> MatchKeywords(ProtocolServerBase server, IEnumerable<TagFilter> tagFilters, IEnumerable<string> keyWords)
        {
            if (keyWords?.Any() != true && tagFilters?.Any() != true)
            {
                return new Tuple<bool, MatchResults>(true, null);
            }

            // check tags
            {
                bool bTagMatched = true;
                foreach (var tagFilter in tagFilters)
                {
                    if (tagFilter.IsIncluded != server.Tags.Any(x => String.Equals(x, tagFilter.TagName, StringComparison.CurrentCultureIgnoreCase)))
                    {
                        bTagMatched = false;
                        break;
                    }
                }

                if (bTagMatched == false)
                {
                    return new Tuple<bool, MatchResults>(false, null);
                }
            }

            // no keyword
            if (keyWords?.Any() != true)
            {
                return new Tuple<bool, MatchResults>(true, null);
            }

            // match keywords
            var dispName = server.DisplayName;
            var subTitle = server.SubTitle;
            var mrs = App.Context.KeywordMatchService.Match(new List<string>() { dispName, subTitle }, keyWords);
            if (mrs.IsMatchAllKeywords)
                return new Tuple<bool, MatchResults>(true, mrs);

            return new Tuple<bool, MatchResults>(false, null);
        }
    }
}
