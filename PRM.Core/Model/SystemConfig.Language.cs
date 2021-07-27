using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using Windows.ApplicationModel.VoiceCommands;
using PRM.Core.Properties;
using Shawn.Utils;

namespace PRM.Core.Model
{
    public sealed class SystemConfigLanguage : SystemConfigBase
    {
        public const string DefaultLanguageCode = "en-us";
        private readonly ResourceDictionary _appResourceDictionary = null;

        private readonly ResourceDictionary _defaultLanguageResourceDictionary = null;
        private ResourceDictionary _currentLanguageResourceDictionary = null;
        private readonly Dictionary<string, string> _languageCode2Name = new Dictionary<string, string>();
        private readonly Dictionary<string, ResourceDictionary> _languageCode2Resources = new Dictionary<string, ResourceDictionary>();

        /// <summary>
        /// code => language file name, all codes leave in small cases, ref https://en.wikipedia.org/wiki/Language_code
        /// </summary>
        public Dictionary<string, string> LanguageCode2Name => _languageCode2Name;

        private string _currentLanguageCode = "en-us";

        public string CurrentLanguageCode
        {
            get => _currentLanguageCode;
            set
            {
                if (ChangeLanguage(value))
                {
                    SetAndNotifyIfChanged(nameof(CurrentLanguageCode), ref _currentLanguageCode, value);
                }
            }
        }

        public SystemConfigLanguage(ResourceDictionary appResourceDictionary, Ini ini) : base(ini)
        {
            _appResourceDictionary = appResourceDictionary;
            Debug.Assert(appResourceDictionary != null);

            Init();
            _defaultLanguageResourceDictionary = GetResourceDictionaryByCode(DefaultLanguageCode);
            _currentLanguageResourceDictionary = _defaultLanguageResourceDictionary;
            Load();
        }

        private void Init()
        {
            _languageCode2Name.Clear();
            _languageCode2Resources.Clear();

            // add static language resources
            AddStaticLanguageResources("en-us");
            AddStaticLanguageResources("zh-cn");
            AddStaticLanguageResources("de-de");
            AddStaticLanguageResources("fr-fr");

#if DEV
            // check if any fields missing in the LanguageResources.

            var en = GetResourceDictionaryByCode("en-us");
            var zh_cn = GetResourceDictionaryByCode("zh-cn");
            var de_de = GetResourceDictionaryByCode("de-de");
            var fr_fr = GetResourceDictionaryByCode("fr-fr");

            Debug.Assert(MultiLangHelper.FindMissingFields(en, zh_cn).Count == 0);
            Debug.Assert(MultiLangHelper.FindMissingFields(en, de_de).Count == 0);
            Debug.Assert(MultiLangHelper.FindMissingFields(en, fr_fr).Count == 0);
#endif
        }

        public void AddXamlLanguageResources(string code, string fullName)
        {
            var resourceDictionary = GetResourceDictionaryByXamlFilePath(fullName);
            if (resourceDictionary?.Contains("language_name") != true) return;
            AddOrUpdateLanguage(code, resourceDictionary["language_name"].ToString(), resourceDictionary);
        }

        private void AddStaticLanguageResources(string code)
        {
            //var path = $"pack://application:,,,/PRM.Core;component/Languages/en_us.xaml";
            var path = $"pack://application:,,,/PRM.Core;component/Languages/{code}.xaml";
            if (_languageCode2Name.ContainsKey(code)) return;
            var r = GetResourceDictionaryByXamlUri(path);
            Debug.Assert(r != null);
            Debug.Assert(r.Contains("language_name"));
            AddOrUpdateLanguage(code, r["language_name"].ToString(), r);
        }

        [CanBeNull]
        private static ResourceDictionary GetResourceDictionaryByXamlUri(string path)
        {
            try
            {
                var resourceDictionary = MultiLangHelper.LangDictFromXamlUri(new Uri(path));
                if (resourceDictionary != null)
                {
                    return resourceDictionary;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return null;
        }

        [CanBeNull]
        private static ResourceDictionary GetResourceDictionaryByJsonFilePath(string path)
        {
            Debug.Assert(path.EndsWith(".json", true, CultureInfo.InstalledUICulture));
            try
            {
                var resourceDictionary = MultiLangHelper.LangDictFromJsonFile(path);
                if (resourceDictionary != null)
                {
                    return resourceDictionary;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return null;
        }

        [CanBeNull]
        private static ResourceDictionary GetResourceDictionaryByXamlFilePath(string path)
        {
            Debug.Assert(path.EndsWith(".xaml", true, CultureInfo.InstalledUICulture));
            try
            {
                var resourceDictionary = MultiLangHelper.LangDictFromXamlFile(path);
                if (resourceDictionary != null)
                {
                    return resourceDictionary;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return null;
        }

        /// <summary>
        /// may return null
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        [CanBeNull]
        public ResourceDictionary GetResourceDictionaryByCode(string code)
        {
            return _languageCode2Resources.ContainsKey(code) ? _languageCode2Resources[code] : null;
        }

        private void AddOrUpdateLanguage(string code, string name, ResourceDictionary resourceDictionary)
        {
            if (_languageCode2Name.ContainsKey(code))
            {
                _languageCode2Name[code] = name;
                _languageCode2Resources[code] = resourceDictionary;
            }
            else
            {
                _languageCode2Name.Add(code, name);
                _languageCode2Resources.Add(code, resourceDictionary);
            }
            RaisePropertyChanged(nameof(LanguageCode2Name));
        }

        public string GetText(string textKey)
        {
            Debug.Assert(_currentLanguageResourceDictionary != null);
            Debug.Assert(_defaultLanguageResourceDictionary != null);
            if (_currentLanguageResourceDictionary.Contains(textKey))
                return _currentLanguageResourceDictionary[textKey].ToString();
            if (_defaultLanguageResourceDictionary.Contains(textKey))
                return _defaultLanguageResourceDictionary[textKey].ToString();

            SimpleLogHelper.Error("GetText: can't find any string by the key '" + textKey + "'!");
            throw new NotImplementedException("can't find any string by '" + textKey + "'!");
        }

        public bool ChangeLanguage(string code)
        {
            var newVal = code?.Trim()?.ToLower();
            if (_currentLanguageCode == newVal)
                return false;
            if (!_languageCode2Name.ContainsKey(newVal))
                return false;

            // reload ResourceDictionary
            var tmp = GetResourceDictionaryByCode(newVal);
            if (tmp == null)
            {
                return false;
            }

            _currentLanguageCode = newVal;
            _currentLanguageResourceDictionary = tmp;
            _appResourceDictionary?.ChangeLanguage(_currentLanguageResourceDictionary);
            GlobalEventHelper.OnLanguageChanged?.Invoke();
            return true;
        }

        #region Interface

        private const string _sectionName = "General";

        public override void Save()
        {
            _ini.WriteValue("lang", _sectionName, CurrentLanguageCode);
            _ini.Save();
        }

        public override void Load()
        {
            if (!_ini.ContainsKey("lang", _sectionName))
                return;
            StopAutoSave = true;
            CurrentLanguageCode = _ini.GetValue("lang", _sectionName, DefaultLanguageCode);
            StopAutoSave = false;
        }

        public override void Update(SystemConfigBase newConfig)
        {
            UpdateBase(this, newConfig, typeof(SystemConfigLanguage));
        }

        #endregion Interface
    }
}