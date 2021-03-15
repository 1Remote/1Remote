using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VariableKeywordMatcher;
using VariableKeywordMatcher.Model;
using VariableKeywordMatcher.Provider.DirectMatch;
using VariableKeywordMatcher.Provider.DiscreteMatch;

namespace PRM.Core.Model
{

    public class KeywordMatchService
    {
        public class Cache
        {
            public Cache(MatchCache matchCache)
            {
                _matchCache = matchCache;
                _accessTime = DateTime.Now;
            }

            private DateTime _accessTime;
            private MatchCache _matchCache;

            public ref MatchCache GetMatchCache()
            {
                _accessTime = DateTime.Now;
                return ref _matchCache;
            }

            public DateTime GetAccessTime() => _accessTime;
        }

        private readonly Dictionary<string, Cache> _caches = new Dictionary<string, Cache>(500);

        private Matcher _matcher = null;

        public KeywordMatchService()
        {
            var pns = new string[]
            {
                DirectMatchProvider.GetName(),
                DiscreteMatchProvider.GetName(),
            };
            _matcher = Builder.Build(pns, false);
        }


        public void Init(string[] providerNames)
        {
            Debug.Assert(providerNames.Length > 0);
            _matcher = Builder.Build(providerNames, false);
        }

        private void CleanUp()
        {
            if (_caches.Any(x => x.Value.GetAccessTime() < DateTime.Now.AddHours(-24)))
            {
                var kvs = _caches.Where(x => x.Value.GetAccessTime() < DateTime.Now.AddHours(-12))?
                    .OrderBy(x => x.Value.GetAccessTime())?.ToArray();
                foreach (var kv in kvs)
                {
                    _caches.Remove(kv.Key);
                }
            }
        }

        public MatchResult Match(string originalString, List<string> keywords)
        {
            CleanUp();
            var cache = GetCache(originalString);
            return _matcher.Match(cache, keywords);
        }

        public MatchResult Match(string originalString, string keyword)
        {
            CleanUp();
            var cache = GetCache(originalString);
            return _matcher.Match(cache, new[] { keyword });
        }

        public MatchResults Matchs(List<string> originalStrings, IEnumerable<string> keywords)
        {
            CleanUp();
            var caches = originalStrings.Select(x => GetCache(x)).ToList();
            return _matcher.Matchs(caches, keywords);
        }

        private ref MatchCache GetCache(string originalString)
        {
            if (!_caches.ContainsKey(originalString))
            {
                var cache = new MatchCache(originalString);
                _caches.Add(originalString, new Cache(cache));
            }
            return ref _caches[originalString].GetMatchCache();
        }

        public void UpdateMatchCache(string originalString)
        {
            var newCache = _matcher.CreateStringCache(originalString);
            GetCache(originalString).SpellCaches = newCache.SpellCaches;
        }

        //public static List<bool> MergeHitFlags(List<List<bool>> flagss)
        //{
        //    var mergedFlags = new List<bool>();
        //    for (var i = 0; i < flagss.First().Count; i++)
        //        mergedFlags.Add(false);
        //    foreach (var flags in flagss)
        //    {
        //        for (int j = 0; j < flags.Count; j++)
        //            mergedFlags[j] |= flags[j];
        //    }
        //    return mergedFlags;
        //}
    }
}
