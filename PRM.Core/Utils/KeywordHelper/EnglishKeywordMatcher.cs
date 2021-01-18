using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PRM.Core.Utils.KeywordHelper
{
    public class EnglishKeywordMatcher: IKeywordMatcher
    {
        public string GetMatcherName()
        {
            return this.GetType().Name;
        }

        public string GetMatcherDescription()
        {
            return "english keyword matcher, can handel only utf-8 chars";
        }


        private enum SymbolForMatchType
        {
            Original,
            InitialInLowCase,
        }

        private class SymbolForMatch
        {
            public SymbolForMatchType Type;
            public string Value;
            public List<int> ValueCharIndexToOriginalString;
        }

        public const string Separator = "";

        public object GenerateSymbolForMatch(string originalString)
        {
            if (string.IsNullOrWhiteSpace(originalString))
            {
                return null;
            }

            // build original
            var ret = new List<SymbolForMatch>();
            ret.Add(new SymbolForMatch()
            {
                Type = SymbolForMatchType.Original,
                Value =  originalString,
                ValueCharIndexToOriginalString = new List<int>(),
            });
            for (int i = 0; i < originalString.Length; i++)
            {
                ret.Last().ValueCharIndexToOriginalString.Add(i);
            }

            var strings = originalString.Split(new string[] { Separator }, StringSplitOptions.RemoveEmptyEntries);
            if (strings.Length > 1)
            {
                // build Initial
                var symbolInitial = new SymbolForMatch()
                {
                    Type = SymbolForMatchType.InitialInLowCase,
                };
                var sb = new StringBuilder();
                var index = new List<int>();
                for (int i = 0; i < originalString.Length; i++)
                {
                    int j = i;
                    while (originalString[j] != ' ')
                    {
                        sb.Append(originalString[j].ToString().ToLower());
                        index.Add(j);
                        break;
                    }
                    while (originalString[j] != ' ')
                    {
                        ++j;
                    }


                    if (originalString[i] == ' ')
                    {
                        
                    }
                }


                for (int i = 0; i < strings.Length; i++)
                {
                    sb.Append(strings[i][0].ToString().ToLower());
                    //index.Add();
                }
            }
            return originalString;
        }



        public bool IsMatch(string keywordWithWhiteSpaceDelimiter, object symbolForMatch, out List<bool> matchedPlace,
            bool isCaseSensitive = false)
        {
            matchedPlace = new List<bool>();
            if (!(symbolForMatch is string orgString))
                return false;
            if (string.IsNullOrWhiteSpace(orgString))
                return false;

            matchedPlace = new List<bool>(orgString.Length);




            return false;
        }
    }
}
