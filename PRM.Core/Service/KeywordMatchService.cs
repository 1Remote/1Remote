using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using VariableKeywordMatcher;
using VariableKeywordMatcher.Model;
using VariableKeywordMatcher.Provider.ChineseZhCnPinYin;
using VariableKeywordMatcher.Provider.ChineseZhCnPinYinInitials;
using VariableKeywordMatcher.Provider.DirectMatch;
using VariableKeywordMatcher.Provider.DiscreteMatch;
using VariableKeywordMatcher.Provider.InitialsMatch;

namespace PRM.Core.Service
{
    public class MatchProviderInfo : NotifyPropertyChangedBase
    {

        private string _name;
        public string Name
        {
            get => _name;
            set => SetAndNotifyIfChanged(nameof(Name), ref _name, value);
        }


        private string _title1 = "";
        public string Title1
        {
            get => _title1;
            set => SetAndNotifyIfChanged(nameof(Title1), ref _title1, value);
        }



        private string _title2 = "";
        public string Title2
        {
            get => _title2;
            set => SetAndNotifyIfChanged(nameof(Title2), ref _title2, value);
        }


        private bool _enabled = true;
        public bool Enabled
        {
            get => _enabled;
            set => SetAndNotifyIfChanged(nameof(Enabled), ref _enabled, value);
        }

        private bool _isEditable = true;
        public bool IsEditable
        {
            get => _isEditable;
            set => SetAndNotifyIfChanged(nameof(IsEditable), ref _isEditable, value);
        }
    }

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

        public MatchResults Match(List<string> originalStrings, IEnumerable<string> keywords)
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

        public static List<MatchProviderInfo> GetMatchProviderInfos()
        {
            var providerNames = VariableKeywordMatcherIn1.Builder.GetAvailableProviderNames();
            var matchProviderInfos = new List<MatchProviderInfo>(providerNames.Count());
            foreach (var enumProviderType in providerNames)
            {
                matchProviderInfos.Add(new MatchProviderInfo()
                {
                    Name = enumProviderType,
                    Title1 = VariableKeywordMatcherIn1.Builder.GetProviderDescription(enumProviderType),
                    Title2 = VariableKeywordMatcherIn1.Builder.GetProviderDescriptionEn(enumProviderType),
                    Enabled = false,
                });
            }
            // first time init.
            CultureInfo ci = CultureInfo.CurrentCulture;
            string code = ci.Name.ToLower();
            foreach (var matchProviderInfo in matchProviderInfos)
            {
                matchProviderInfo.Enabled = false;
            }

            var setEnabled = new Action<string, bool, bool>((name, isEnabled, isEditable) =>
            {
                if (matchProviderInfos.Any(x => x.Name == name))
                {
                    matchProviderInfos.First(x => x.Name == name).Enabled = isEnabled;
                    matchProviderInfos.First(x => x.Name == name).IsEditable = isEditable;
                }
            });

            setEnabled(DirectMatchProvider.GetName(), true, false);
            setEnabled(InitialsMatchProvider.GetName(), true, true);
            setEnabled(DiscreteMatchProvider.GetName(), true, true);

            if (code.StartsWith("zh"))
            {
                setEnabled(ChineseZhCnPinYinMatchProvider.GetName(), true, true);
                setEnabled(ChineseZhCnPinYinInitialsMatchProvider.GetName(), true, true);
            }
            return matchProviderInfos;
        }
    }
}
