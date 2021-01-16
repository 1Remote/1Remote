using System.Collections.Generic;

namespace PRM.Core.Utils.KeywordHelper
{
    public interface IKeywordMatcher
    {
        string GetMatcherName();
        string GetMatcherDescription();

        object GenerateSymbolForMatch(string originalString);

        bool IsMatch(string keywordWithWhiteSpaceDelimiter, object symbolForMatch, out List<bool> matchedPlace, bool isCaseSensitive = false);
    }
}