using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using Shawn.Utils;
using VariableKeywordMatcher;
using VariableKeywordMatcher.Provider.DirectMatch;

namespace PRM.Core.Model
{
    public sealed class SystemConfigKeywordMatch : SystemConfigBase
    {
        private readonly PrmContext _context;

        public SystemConfigKeywordMatch(PrmContext context, Ini ini) : base(ini)
        {
            _context = context;
            Load();
        }

        private List<MatchProviderInfo> _availableMatcherProviders;

        public List<MatchProviderInfo> AvailableMatcherProviders
        {
            get => _availableMatcherProviders;
            set => SetAndNotifyIfChanged(nameof(AvailableMatcherProviders), ref _availableMatcherProviders, value);
        }

        #region Interface

        private const string _sectionName = "KeywordMatch";

        public override void Save()
        {
            var enabledNames = string.Join(";", AvailableMatcherProviders.Where(x => x.Enabled).Select(x => x.Name));
            var oldValue = _ini.GetValue("EnableProviders".ToLower(), _sectionName, "");
            if (oldValue != enabledNames)
            {
                _ini.WriteValue("EnableProviders".ToLower(), _sectionName, enabledNames);
                _ini.Save();
                _context.KeywordMatchService.Init(AvailableMatcherProviders.Where(x => x.Enabled).Select(x => x.Name).ToArray());
            }
        }

        public override void Load()
        {
            var providerNames = VariableKeywordMatcher.Builder.GetAvailableProviderNames();
            var matchProviderInfos = new List<MatchProviderInfo>(providerNames.Count());
            foreach (var enumProviderType in providerNames)
            {
                matchProviderInfos.Add(new MatchProviderInfo()
                {
                    Name = enumProviderType,
                    Title1 = VariableKeywordMatcher.Builder.GetProviderDescription(enumProviderType),
                    Title2 = VariableKeywordMatcher.Builder.GetProviderDescriptionEn(enumProviderType),
                    Enabled = false,
                });
            }

            StopAutoSave = true;

            var enabledNames = _ini.GetValue("EnableProviders".ToLower(), _sectionName, "")
                .Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);

            if (enabledNames?.Length > 0)
            {
                foreach (var enabledName in enabledNames)
                {
                    var first = matchProviderInfos.FirstOrDefault(x => x.Name == enabledName);
                    if (first != null)
                        first.Enabled = true;
                }
            }
            else
            {
                foreach (var matchProviderInfo in matchProviderInfos)
                {
                    matchProviderInfo.Enabled = true;
                }
            }

            // ReSharper disable once PossibleNullReferenceException
            matchProviderInfos.FirstOrDefault(x => x.Name == DirectMatchProvider.GetName()).Enabled = true;
            // ReSharper disable once PossibleNullReferenceException
            matchProviderInfos.FirstOrDefault(x => x.Name == DirectMatchProvider.GetName()).IsEditable = false;

            StopAutoSave = false;

            AvailableMatcherProviders = matchProviderInfos;
            _context.KeywordMatchService.Init(AvailableMatcherProviders.Where(x => x.Enabled).Select(x => x.Name).ToArray());
        }

        public override void Update(SystemConfigBase newConfig)
        {
            UpdateBase(this, newConfig, typeof(SystemConfigKeywordMatch));
        }

        #endregion Interface
    }
}